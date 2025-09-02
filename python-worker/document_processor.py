import os
import re
import logging
from typing import List, Optional
import PyPDF2
import fitz  # PyMuPDF
import markdown

logger = logging.getLogger(__name__)

class DocumentProcessor:
    def __init__(self, chunk_size: int = 1000, chunk_overlap: int = 200):
        self.chunk_size = chunk_size
        self.chunk_overlap = chunk_overlap
    
    def extract_text(self, file_path: str) -> Optional[str]:
        """Dosya tipine göre text çıkar"""
        file_ext = os.path.splitext(file_path)[1].lower()
        
        try:
            if file_ext == '.pdf':
                return self._extract_pdf_text(file_path)
            elif file_ext == '.md':
                return self._extract_markdown_text(file_path)
            else:
                logger.warning(f"Desteklenmeyen dosya tipi: {file_ext}")
                return None
        except Exception as e:
            logger.error(f"Text extraction hatası {file_path}: {e}")
            return None
    
    def _extract_pdf_text(self, file_path: str) -> Optional[str]:
        """PDF'den text çıkar"""
        try:
            # PyMuPDF ile text extraction (daha iyi sonuç)
            doc = fitz.open(file_path)
            text_content = ""
            
            for page_num in range(doc.page_count):
                page = doc[page_num]
                text_content += page.get_text()
            
            doc.close()
            
            # Text cleaning
            text_content = self._clean_text(text_content)
            logger.info(f"PDF text extraction tamamlandı: {len(text_content)} karakter")
            return text_content
            
        except Exception as e:
            logger.error(f"PDF text extraction hatası: {e}")
            # Fallback: PyPDF2
            try:
                with open(file_path, 'rb') as file:
                    pdf_reader = PyPDF2.PdfReader(file)
                    text_content = ""
                    
                    for page in pdf_reader.pages:
                        text_content += page.extract_text()
                    
                    text_content = self._clean_text(text_content)
                    logger.info(f"PDF text extraction (PyPDF2) tamamlandı: {len(text_content)} karakter")
                    return text_content
                    
            except Exception as e2:
                logger.error(f"PDF text extraction (PyPDF2) hatası: {e2}")
                return None
    
    def _extract_markdown_text(self, file_path: str) -> Optional[str]:
        """Markdown dosyasından text çıkar"""
        try:
            with open(file_path, 'r', encoding='utf-8') as file:
                markdown_content = file.read()
            
            # Markdown'ı HTML'e çevir
            html = markdown.markdown(markdown_content)
            
            # HTML tag'lerini temizle
            text_content = re.sub(r'<[^>]+>', '', html)
            
            # Text cleaning
            text_content = self._clean_text(text_content)
            logger.info(f"Markdown text extraction tamamlandı: {len(text_content)} karakter")
            return text_content
            
        except Exception as e:
            logger.error(f"Markdown text extraction hatası: {e}")
            return None
    
    def _clean_text(self, text: str) -> str:
        """Text'i temizle"""
        # Fazla boşlukları temizle
        text = re.sub(r'\s+', ' ', text)
        
        # Satır sonlarını normalize et
        text = re.sub(r'\n+', '\n', text)
        
        # Başta ve sonda boşlukları temizle
        text = text.strip()
        
        return text
    
    def chunk_text(self, text: str) -> List[str]:
        """Text'i chunk'lara böl"""
        if not text:
            return []
        
        chunks = []
        start = 0
        
        while start < len(text):
            # Chunk sonunu belirle
            end = start + self.chunk_size
            
            if end >= len(text):
                # Son chunk
                chunk = text[start:]
                if chunk.strip():
                    chunks.append(chunk.strip())
                break
            
            # Kelime sınırında böl
            chunk = text[start:end]
            last_space = chunk.rfind(' ')
            
            if last_space > start + self.chunk_size // 2:
                # Kelime sınırında böl
                chunk = text[start:start + last_space]
                start = start + last_space + 1
            else:
                # Kelime sınırı bulunamadı, zorla böl
                start = end
            
            if chunk.strip():
                chunks.append(chunk.strip())
            
            # Overlap için start'ı geri al
            start = max(0, start - self.chunk_overlap)
        
        logger.info(f"Text {len(chunks)} chunk'a bölündü")
        return chunks
