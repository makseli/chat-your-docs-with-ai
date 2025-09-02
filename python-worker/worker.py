import os
import json
import redis
import logging
import time
from typing import Dict, List, Optional
from datetime import datetime
from document_processor import DocumentProcessor
from vector_store import VectorStore

# Logging configuration
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

class RedisWorker:
    def __init__(self):
        self.redis_client = redis.Redis(
            host=os.getenv('REDIS_HOST', 'redis'),
            port=int(os.getenv('REDIS_PORT', 6379)),
            decode_responses=True
        )
        self.queue_name = 'file_processing_queue'
        self.logs_list_name = 'application_logs'
        self.document_processor = DocumentProcessor()
        self.vector_store = VectorStore()
    
    def log_to_redis(self, level: str, event: str, message: str, details: str = None, file_name: str = None, file_path: str = None, file_size: int = None, error: str = None):
        """Redis'e log kaydı at"""
        try:
            # Backend ile uyumlu format
            log_entry = {
                "Timestamp": datetime.utcnow().isoformat() + "Z",
                "Level": level,
                "Event": event,
                "Message": message,
                "Details": details,
                "FileName": file_name,
                "FilePath": file_path,
                "FileSize": file_size,
                "Error": error
            }
            
            # Redis'e log ekle (sadece null olmayan değerleri ekle)
            filtered_entry = {k: v for k, v in log_entry.items() if v is not None}
            
            self.redis_client.lpush(self.logs_list_name, json.dumps(filtered_entry))
            # Son 1000 log'u tut
            self.redis_client.ltrim(self.logs_list_name, 0, 999)
            
        except Exception as e:
            logger.error(f"Redis log kaydetme hatası: {e}")
        
    def start_worker(self):
        """Ana worker döngüsü"""
        logger.info("Python worker başlatıldı")
        
        # Worker başlatma log'u
        self.log_to_redis(
            level="INFO",
            event="PYTHON_WORKER_STARTED",
            message="Python worker başlatıldı",
            details="Vector processing worker aktif"
        )
        
        last_heartbeat = time.time()
        
        while True:
            try:
                # Her 2 dakikada bir heartbeat log'u gönder
                current_time = time.time()
                if current_time - last_heartbeat >= 120:  # 2 dakika
                    self.log_to_redis(
                        level="INFO",
                        event="PYTHON_WORKER_HEARTBEAT",
                        message="Python worker aktif",
                        details="Worker çalışıyor ve job bekliyor"
                    )
                    last_heartbeat = current_time
                
                # Redis'ten job al (blocking pop)
                job_data = self.redis_client.brpop(self.queue_name, timeout=10)
                
                if job_data:
                    queue_name, job_json = job_data
                    self.process_job(job_json)
                else:
                    logger.debug("Redis'ten job alınamadı, 10 saniye bekleniyor...")
                    
            except redis.ConnectionError:
                logger.error("Redis bağlantı hatası, 5 saniye bekleniyor...")
                time.sleep(5)
            except Exception as e:
                logger.error(f"Worker hatası: {e}")
                time.sleep(1)
    
    def process_job(self, job_json: str):
        """Tek bir job'ı işle"""
        try:
            job_data = json.loads(job_json)
            job_id = job_data.get('id')
            file_name = job_data.get('fileName')
            file_path = job_data.get('filePath')
            
            logger.info(f"Job işleniyor: {job_id} - {file_name}")
            
            # Dosya tipini kontrol et
            if not self.is_supported_file(file_name):
                logger.warning(f"Desteklenmeyen dosya tipi: {file_name}")
                return
            
            # Dosya var mı kontrol et
            if not os.path.exists(file_path):
                logger.error(f"Dosya bulunamadı: {file_path}")
                return
            
            # Dosyayı işle
            self.process_document(file_path, file_name, job_id)
            
            logger.info(f"Job tamamlandı: {job_id} - {file_name}")
            
        except json.JSONDecodeError:
            logger.error(f"Geçersiz job JSON: {job_json}")
        except Exception as e:
            logger.error(f"Job işleme hatası: {e}")
    
    def is_supported_file(self, file_name: str) -> bool:
        """Desteklenen dosya tiplerini kontrol et"""
        supported_extensions = ['.pdf', '.md']
        file_ext = os.path.splitext(file_name)[1].lower()
        return file_ext in supported_extensions
    
    def process_document(self, file_path: str, file_name: str, job_id: str):
        """Dokümanı işle ve vektörize et"""
        file_size = None
        chunks_count = 0
        error_message = None
        
        try:
            # Dosya boyutunu al
            file_size = os.path.getsize(file_path)
            
            # Text extraction
            text_content = self.document_processor.extract_text(file_path)
            if not text_content:
                error_message = "Text içeriği bulunamadı"
                logger.warning(f"Text içeriği bulunamadı: {file_name}")
                self.log_to_redis(
                    level="WARNING",
                    event="FILE_PROCESSING_FAILED",
                    message=f"Dosya işlenemedi: {file_name}",
                    details=f"Text extraction başarısız - {error_message}",
                    file_name=file_name,
                    file_path=file_path,
                    file_size=file_size,
                    error=error_message
                )
                return
            
            # Text chunking
            chunks = self.document_processor.chunk_text(text_content)
            chunks_count = len(chunks)
            logger.info(f"{file_name} için {chunks_count} chunk oluşturuldu")
            
            # Vector generation ve storage
            for i, chunk in enumerate(chunks):
                vector_data = {
                    'job_id': job_id,
                    'file_name': file_name,
                    'file_path': file_path,
                    'chunk_index': i,
                    'chunk_text': chunk,
                    'chunk_size': len(chunk)
                }
                
                # Embedding oluştur
                embedding = self.vector_store.generate_embedding(chunk)
                vector_data['embedding'] = embedding
                
                # Vector'ı kaydet
                self.vector_store.save_vector(vector_data)
            
            # Metadata'yı güncelle
            self.vector_store.update_metadata(job_id, file_name, chunks_count)
            
            # Başarılı işlem log'u
            self.log_to_redis(
                level="INFO",
                event="FILE_PROCESSING_COMPLETED",
                message=f"Dosya başarıyla işlendi: {file_name}",
                details=f"Job ID: {job_id}, Chunks: {chunks_count}, File Size: {file_size} bytes",
                file_name=file_name,
                file_path=file_path,
                file_size=file_size
            )
            
            logger.info(f"Vector işleme tamamlandı: {file_name} - {chunks_count} vector")
            
        except Exception as e:
            error_message = str(e)
            logger.error(f"Doküman işleme hatası {file_name}: {e}")
            
            # Hata log'u
            self.log_to_redis(
                level="ERROR",
                event="FILE_PROCESSING_ERROR",
                message=f"Dosya işleme hatası: {file_name}",
                details=f"Job ID: {job_id}, Error: {error_message}",
                file_name=file_name,
                file_path=file_path,
                file_size=file_size,
                error=error_message
            )

if __name__ == "__main__":
    worker = RedisWorker()
    worker.start_worker()
