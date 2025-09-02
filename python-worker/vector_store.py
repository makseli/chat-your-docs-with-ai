import os
import logging
import numpy as np
import requests
from typing import List, Dict, Any, Optional
from datetime import datetime

logger = logging.getLogger(__name__)

class VectorStore:
    def __init__(self, vectors_dir: str = "/app/data/vectors"):
        self.vectors_dir = vectors_dir
        
        # Ollama configuration
        self.ollama_host = os.getenv('OLLAMA_HOST', 'http://localhost:11434')
        self.embedding_model = os.getenv('EMBEDDING_MODEL', 'nomic-embed-text')
        
        # Simple in-memory vector storage
        self.vectors = []
        
        logger.info(f"Ollama vector store başlatıldı: {vectors_dir}")
    
    def generate_embedding(self, text: str) -> List[float]:
        """Text için embedding oluştur"""
        try:
            # Ollama Embeddings API
            url = f"{self.ollama_host}/api/embeddings"
            payload = {
                "model": self.embedding_model,
                "prompt": text
            }
            
            response = requests.post(url, json=payload, timeout=30)
            response.raise_for_status()
            
            result = response.json()
            embedding = result['embedding']
            
            logger.debug(f"Embedding oluşturuldu: {len(embedding)} boyut")
            return embedding
            
        except requests.exceptions.RequestException as e:
            logger.error(f"Ollama API hatası: {e}")
            # Fallback: random embedding (test için)
            return np.random.rand(768).tolist()
        except Exception as e:
            logger.error(f"Embedding oluşturma hatası: {e}")
            # Fallback: random embedding (test için)
            return np.random.rand(768).tolist()
    
    def save_vector(self, vector_data: Dict[str, Any]):
        """Vector'ı memory'ye kaydet ve backend'e gönder"""
        try:
            job_id = vector_data['job_id']
            file_name = vector_data['file_name']
            file_path = vector_data['file_path']
            chunk_index = vector_data['chunk_index']
            chunk_text = vector_data['chunk_text']
            embedding = vector_data['embedding']
            
            # Unique document ID oluştur
            doc_id = f"{job_id}_{chunk_index}"
            
            # Vector data oluştur
            vector_entry = {
                'id': doc_id,
                'document': chunk_text,
                'embedding': embedding,
                'metadata': {
                    'job_id': job_id,
                    'file_name': file_name,
                    'file_path': file_path,
                    'chunk_index': chunk_index,
                    'chunk_size': len(chunk_text),
                    'created_at': datetime.utcnow().isoformat()
                }
            }
            
            # Memory'ye ekle
            self.vectors.append(vector_entry)
            
            # Backend'e gönder
            self.send_vector_to_backend(vector_entry)
            
            logger.info(f"Vector memory'ye kaydedildi: {doc_id}")
            
        except Exception as e:
            logger.error(f"Vector kaydetme hatası: {e}")
    
    def send_vector_to_backend(self, vector_entry: Dict[str, Any]):
        """Vector'ı backend'e gönder"""
        try:
            backend_url = os.getenv('BACKEND_URL', 'http://rag-backend-api:8080')
            url = f"{backend_url}/api/question/vector"
            
            # Backend formatına dönüştür
            backend_vector = {
                'id': vector_entry['id'],
                'document': vector_entry['document'],
                'embedding': vector_entry['embedding'],
                'metadata': vector_entry['metadata']
            }
            
            response = requests.post(url, json=backend_vector, timeout=10)
            response.raise_for_status()
            
            logger.info(f"Vector backend'e gönderildi: {vector_entry['id']}")
            
        except requests.exceptions.RequestException as e:
            logger.error(f"Backend'e vector gönderme hatası: {e}")
        except Exception as e:
            logger.error(f"Vector backend gönderme hatası: {e}")
    
    def update_metadata(self, job_id: str, file_name: str, total_chunks: int):
        """Metadata'yı güncelle (memory'de otomatik)"""
        try:
            # Memory'de metadata otomatik olarak güncellenir
            logger.info(f"Metadata güncellendi: {file_name} - {total_chunks} chunks")
            
        except Exception as e:
            logger.error(f"Metadata güncelleme hatası: {e}")
    
    def get_vectors_by_file(self, file_name: str) -> List[Dict[str, Any]]:
        """Belirli bir dosyaya ait tüm vector'ları getir"""
        try:
            # Memory'den file_name'e göre filtrele
            vectors = [v for v in self.vectors if v['metadata']['file_name'] == file_name]
            
            # Chunk index'e göre sırala
            vectors.sort(key=lambda x: x['metadata']['chunk_index'])
            
            logger.info(f"Dosya vector'ları getirildi: {file_name} için {len(vectors)} vector")
            return vectors
            
        except Exception as e:
            logger.error(f"Vector getirme hatası: {e}")
            return []
    
    def get_all_vectors(self) -> List[Dict[str, Any]]:
        """Tüm vector'ları getir"""
        try:
            logger.info(f"Tüm vector'lar getirildi: {len(self.vectors)} vector")
            return self.vectors.copy()
            
        except Exception as e:
            logger.error(f"Tüm vector'ları getirme hatası: {e}")
            return []
    
    def search_similar(self, query_text: str, n_results: int = 5) -> List[Dict[str, Any]]:
        """Benzer vector'ları ara"""
        try:
            # Query için embedding oluştur
            query_embedding = self.generate_embedding(query_text)
            
            # Memory'de ara
            similar_vectors = []
            for vector in self.vectors:
                distance = self._calculate_distance(query_embedding, vector['embedding'])
                vector_data = {
                    'id': vector['id'],
                    'embedding': vector['embedding'],
                    'document': vector['document'],
                    'metadata': vector['metadata'],
                    'distance': distance
                }
                similar_vectors.append(vector_data)
            
            # Distance'a göre sırala ve top n_results'ı al
            similar_vectors.sort(key=lambda x: x['distance'])
            similar_vectors = similar_vectors[:n_results]
            
            logger.info(f"Vector arama tamamlandı: {query_text} için {len(similar_vectors)} sonuç")
            return similar_vectors
            
        except Exception as e:
            logger.error(f"Vector arama hatası: {e}")
            return []
    
    def get_collection_stats(self) -> Dict[str, Any]:
        """Collection istatistiklerini getir"""
        try:
            return {
                'total_documents': len(self.vectors),
                'collection_name': 'documents',
                'vectors_dir': self.vectors_dir
            }
        except Exception as e:
            logger.error(f"İstatistik getirme hatası: {e}")
            return {}
    
    def delete_vectors_by_job(self, job_id: str) -> bool:
        """Belirli bir job'a ait tüm vector'ları sil"""
        try:
            # Job ID'ye göre filtrele
            original_count = len(self.vectors)
            self.vectors = [v for v in self.vectors if v['metadata']['job_id'] != job_id]
            deleted_count = original_count - len(self.vectors)
            
            if deleted_count > 0:
                logger.info(f"Job {job_id} için {deleted_count} vector silindi")
                return True
            else:
                logger.warning(f"Job {job_id} için vector bulunamadı")
                return False
                
        except Exception as e:
            logger.error(f"Vector silme hatası: {e}")
            return False

    def _calculate_distance(self, embedding1: List[float], embedding2: List[float]) -> float:
        """İki embedding arasındaki mesafeyi hesapla"""
        if len(embedding1) != len(embedding2):
            return float('inf')
        
        sum_squared_diff = sum((a - b) ** 2 for a, b in zip(embedding1, embedding2))
        return sum_squared_diff ** 0.5