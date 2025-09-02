# 🤖 Chat Your Docs with AI

---

## 🇺🇸 English

Chat with your documents! Upload your PDF and Markdown files and have AI-powered Q&A conversations. This project provides an AI assistant that understands your documents and answers your questions using RAG (Retrieval-Augmented Generation) technology.

### 🏗️ System Architecture

#### Technology Stack
- **Backend API**: .NET Core 8.0
- **AI/ML Processing**: Python 3.9
- **LLM**: Ollama (llama3.1:8b + nomic-embed-text)
- **Vector Storage**: In-memory (Ollama-based)
- **Message Queue**: Redis
- **Frontend**: HTML/CSS/JavaScript
- **Containerization**: Docker & Docker Compose

#### Service Structure
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Frontend UI   │    │  .NET Core API  │    │  Python Worker  │
│   (Port 80)     │◄──►│   (Port 8080)   │◄──►│   (Vectorizer)  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                              │                        │
                              ▼                        ▼
                       ┌─────────────────┐    ┌─────────────────┐
                       │     Redis       │    │     Ollama      │
                       │   (Port 6379)   │    │   (Port 11434)  │
                       └─────────────────┘    └─────────────────┘
```

### 🚀 Quick Start

#### 1. Requirements
- Docker & Docker Compose
- At least 8GB RAM (for Ollama models)
- At least 10GB disk space

#### 2. Installation
```bash
# Clone the project
git clone <repository-url>
cd chat-your-docs-with-ai

# Set up permissions
chmod +x setup-permissions.sh
./setup-permissions.sh

# Download Ollama models
docker compose up ollama
# In another terminal:
docker exec -it ollama_llm ollama pull llama3.1:8b
docker exec -it ollama_llm ollama pull nomic-embed-text

# Start all services
docker compose up --build
```

#### 3. Access
- **Frontend**: http://localhost:80
- **Backend API**: http://localhost:8080
- **Ollama**: http://localhost:11434
- **Redis**: localhost:6379

### 📋 API Endpoints

#### Health Check
```http
GET /
```

**Response:**
```json
{
  "status": "healthy",
  "uptime": "2d 5h 30m 15s",
  "timestamp": "2024-12-20T14:30:22Z",
  "services": {
    "redis": {
      "status": "connected",
      "isConnected": true
    },
    "ollama": {
      "status": "connected",
      "isConnected": true,
      "embeddingModel": "nomic-embed-text",
      "chatModel": "llama3.1:8b",
      "modelsAvailable": true
    },
    "pythonWorker": {
      "status": "active",
      "isActive": true,
      "lastActivity": "2024-12-20T14:25:22Z",
      "timeSinceLastActivity": "5.0 minutes ago",
      "totalLogs": 12
    },
    "frontend": {
      "status": "running",
      "isRunning": true,
      "port": 80,
      "message": "Frontend service is running"
    }
  }
}
```

#### File Upload
```http
POST /api/upload
Content-Type: multipart/form-data

Form Data:
- file: PDF or Markdown file
```

#### Question Answering
```http
POST /api/question/ask
Content-Type: application/json

{
  "question": "What information is available in the documents?",
  "maxResults": 5,
  "minRelevance": 0.5
}
```

#### Document Search
```http
POST /api/question/search
Content-Type: application/json

{
  "query": "search term"
}
```

#### System Status
```http
GET /api/question/status
```

#### Application Logs
```http
GET /api/logs?count=50
```

### 🔄 Workflow (Pipeline)

#### 1. File Upload Process
```
1. User uploads PDF/MD file
   ↓
2. .NET Core validates file type & size
   ↓
3. File saved to /data/uploaded_files/
   ↓
4. File hash calculated (duplicate check)
   ↓
5. Job queued to Redis (if not duplicate)
   ↓
6. Response sent to user
```

#### 2. Vectorization Process (Python Worker)
```
1. Python worker polls Redis queue
   ↓
2. Job picked up from queue
   ↓
3. PDF/MD file parsed (text extraction)
   ↓
4. Text chunked (1000 chars, 200 overlap)
   ↓
5. Each chunk embedded via Ollama
   ↓
6. Vectors stored in memory
   ↓
7. Processing logged to Redis
```

#### 3. Q&A Process (RAG Pipeline)
```
1. User asks question via frontend
   ↓
2. .NET Core receives question
   ↓
3. Question embedded via Ollama
   ↓
4. Similar vectors searched in memory
   ↓
5. Top 5 relevant chunks retrieved
   ↓
6. Context built from chunks
   ↓
7. Context + question sent to Ollama LLM
   ↓
8. AI answer generated
   ↓
9. Response with sources sent to user
```

### 🐍 Python Worker Details

#### Responsibilities
- **File Processing**: Parse PDF and Markdown files
- **Text Chunking**: Split large texts into 1000-character chunks
- **Embedding Generation**: Create vectors via Ollama API
- **Vector Storage**: Manage in-memory vector database
- **Redis Logging**: Log processing statuses

#### Supported File Formats
- **PDF**: PyMuPDF (fitz) + PyPDF2 fallback
- **Markdown**: Markdown parser with HTML to text extraction

### 🏗️ .NET Core Backend Details

#### Responsibilities
- **File Upload API**: File upload and validation
- **Question Answering**: RAG pipeline management
- **Vector Search**: Similarity search operations
- **LLM Integration**: Ollama chat completion
- **Redis Management**: Queue and logging
- **Frontend Serving**: Static file serving

### 🎨 Frontend Details

#### Features
- **Modern UI**: Gradient design, responsive layout
- **Real-time Chat**: AJAX API communication
- **Loading States**: Spinner and disabled states
- **Error Handling**: User-friendly error messages
- **Source Attribution**: Source file and chunk information
- **Turkish Language**: Turkish interface

### 🔧 Configuration

#### Environment Variables
```yaml
# .NET Core Backend
STORAGE_TYPE=FileStorage
REDIS_CONNECTION_STRING=redis:6379
OLLAMA_HOST=http://ollama:11434
EMBEDDING_MODEL=nomic-embed-text
CHAT_MODEL=llama3.1:8b

# Python Worker
REDIS_HOST=redis
REDIS_PORT=6379
OLLAMA_HOST=http://ollama:11434
EMBEDDING_MODEL=nomic-embed-text
CHUNK_SIZE=1000
CHUNK_OVERLAP=200
```

### 📊 Monitoring & Logging

#### Log Events
- **APP_STARTED**: Application started
- **REDIS_CONNECTED**: Redis connection established
- **FILE_UPLOADED**: File uploaded
- **FILE_QUEUED**: File queued
- **FILE_PROCESSING_COMPLETED**: File processed
- **FILE_PROCESSING_ERROR**: Processing error
- **PYTHON_WORKER_STARTED**: Worker started

### 🚀 Performance & Scalability

#### Optimizations
- **In-memory storage**: Fast vector access
- **Chunking strategy**: Optimal text processing
- **Async operations**: Non-blocking I/O
- **Connection pooling**: Efficient resource usage
- **Error handling**: Graceful degradation

### 🔒 Security

#### File Security
- **File type validation**: Only PDF/MD
- **File size limits**: Size restrictions
- **Filename sanitization**: Safe file names
- **Duplicate detection**: Hash-based checking

### 🐛 Troubleshooting

#### Common Issues

##### 1. Ollama Model Not Found
```bash
# Check models
docker exec -it ollama_llm ollama list

# Download model
docker exec -it ollama_llm ollama pull llama3.1:8b
docker exec -it ollama_llm ollama pull snowflake-arctic-embed2
```

##### 2. Redis Connection Error
```bash
# Check Redis status
docker logs redis_cache

# Restart Redis
docker restart redis_cache
```

##### 3. File Permission Error
```bash
# Set permissions
./setup-permissions.sh

# Check Docker volumes
docker volume ls
```

### 📈 Future Improvements

#### Planned Features
- [ ] **Authentication**: User management
- [ ] **Rate Limiting**: API throttling
- [ ] **Persistent Storage**: Vector database
- [ ] **Multi-language**: Multi-language support
- [ ] **File Management**: File deletion/editing
- [ ] **Export Features**: Export results
- [ ] **Analytics**: Usage statistics
- [ ] **WebSocket**: Real-time updates

---

## 🇹🇷 Türkçe

Dokümanlarınızla konuşun! PDF ve Markdown dosyalarınızı yükleyin, AI ile soru-cevap yapın. Bu proje, RAG (Retrieval-Augmented Generation) teknolojisi kullanarak dokümanlarınızı anlayan ve sorularınızı yanıtlayan bir AI asistanı sunar.

## 🏗️ Sistem Mimarisi

### Teknoloji Stack
- **Backend API**: .NET Core 8.0
- **AI/ML Processing**: Python 3.9
- **LLM**: Ollama (llama3.1:8b + nomic-embed-text)
- **Vector Storage**: In-memory (Ollama-based)
- **Message Queue**: Redis
- **Frontend**: HTML/CSS/JavaScript
- **Containerization**: Docker & Docker Compose

### Servis Yapısı
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Frontend UI   │    │  .NET Core API  │    │  Python Worker  │
│   (Port 80)     │◄──►│   (Port 8080)   │◄──►│   (Vectorizer)  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                              │                        │
                              ▼                        ▼
                       ┌─────────────────┐    ┌─────────────────┐
                       │     Redis       │    │     Ollama      │
                       │   (Port 6379)   │    │   (Port 11434)  │
                       └─────────────────┘    └─────────────────┘
```

## 🚀 Hızlı Başlangıç

### 1. Gereksinimler
- Docker & Docker Compose
- En az 8GB RAM (Ollama modelleri için)
- En az 10GB disk alanı

### 2. Kurulum
```bash
# Projeyi klonlayın
git clone <repository-url>
cd chat-your-docs-with-ai

# İzinleri ayarlayın
chmod +x setup-permissions.sh
./setup-permissions.sh

# Ollama modellerini indirin
docker compose up ollama
# Başka terminal'de:
docker exec -it ollama_llm ollama pull llama3.1:8b
docker exec -it ollama_llm ollama pull nomic-embed-text

# Tüm servisleri başlatın
docker compose up --build
```

### 3. Erişim
- **Frontend**: http://localhost:80
- **Backend API**: http://localhost:8080
- **Ollama**: http://localhost:11434
- **Redis**: localhost:6379

## 📋 API Endpoints

### File Upload
```http
POST /api/upload
Content-Type: multipart/form-data

Form Data:
- file: PDF veya Markdown dosyası
```

**Response:**
```json
{
  "success": true,
  "fileDetails": {
    "fileName": "document.pdf",
    "filePath": "/app/data/uploaded_files/document.pdf",
    "fileSize": 1024000,
    "fileHash": "sha256hash...",
    "isDuplicate": false,
    "duplicateOf": null
  },
  "queueInfo": {
    "queued": true,
    "jobId": "uuid-here"
  }
}
```

### Question Answering
```http
POST /api/question/ask
Content-Type: application/json

{
  "question": "Dokümanlarda ne hakkında bilgi var?",
  "maxResults": 5,
  "minRelevance": 0.5
}
```

**Response:**
```json
{
  "success": true,
  "answer": "Dokümanlarınızda şu konular hakkında bilgi bulunuyor...",
  "sources": [
    {
      "fileName": "document.pdf",
      "chunkIndex": 2,
      "relevance": 0.95,
      "chunkText": "İlgili metin parçası..."
    }
  ],
  "confidence": 0.87,
  "processingTime": 1.2,
  "contextLength": 2500,
  "retrievedChunks": 5
}
```

### Document Search
```http
POST /api/question/search
Content-Type: application/json

{
  "query": "arama terimi"
}
```

**Response:**
```json
[
  {
    "fileName": "document.pdf",
    "chunkIndex": 1,
    "relevance": 0.92,
    "chunkText": "Arama sonucu metni...",
    "chunkSize": 856,
    "createdAt": "2024-12-20T14:30:22Z"
  }
]
```

### System Status
```http
GET /api/question/status
```

**Response:**
```json
{
  "isReady": true,
  "timestamp": "2024-12-20T14:30:22Z",
  "services": {
    "RAG": true,
    "Ollama": true
  }
}
```

### Application Logs
```http
GET /api/logs?count=50
```

**Response:**
```json
{
  "success": true,
  "totalCount": 25,
  "logs": [
    {
      "timestamp": "2024-12-20T14:30:22Z",
      "level": "INFO",
      "event": "FILE_PROCESSING_COMPLETED",
      "message": "Dosya başarıyla işlendi: document.pdf",
      "details": "Job ID: uuid, Chunks: 8, File Size: 1024000 bytes",
      "fileName": "document.pdf",
      "filePath": "/app/data/uploaded_files/document.pdf",
      "fileSize": 1024000
    }
  ],
  "retrievedAt": "2024-12-20T14:30:22Z"
}
```

### Health Check
```http
GET /
```

**Response:**
```json
{
  "status": "healthy",
  "uptime": "2d 5h 30m 15s",
  "timestamp": "2024-12-20T14:30:22Z",
  "services": {
    "redis": {
      "status": "connected",
      "isConnected": true
    },
    "ollama": {
      "status": "connected",
      "isConnected": true,
      "embeddingModel": "nomic-embed-text",
      "chatModel": "llama3.1:8b",
      "modelsAvailable": true
    },
    "pythonWorker": {
      "status": "active",
      "isActive": true,
      "lastActivity": "2024-12-20T14:25:22Z",
      "timeSinceLastActivity": "5.0 dakika önce",
      "totalLogs": 12
    },
    "frontend": {
      "status": "running",
      "isRunning": true,
      "port": 80,
      "message": "Frontend servisi çalışıyor"
    }
  }
}
```

## 🔄 İş Akışı (Pipeline)

### 1. Dosya Yükleme Süreci
```
1. User uploads PDF/MD file
   ↓
2. .NET Core validates file type & size
   ↓
3. File saved to /data/uploaded_files/
   ↓
4. File hash calculated (duplicate check)
   ↓
5. Job queued to Redis (if not duplicate)
   ↓
6. Response sent to user
```

### 2. Vectorization Süreci (Python Worker)
```
1. Python worker polls Redis queue
   ↓
2. Job picked up from queue
   ↓
3. PDF/MD file parsed (text extraction)
   ↓
4. Text chunked (1000 chars, 200 overlap)
   ↓
5. Each chunk embedded via Ollama
   ↓
6. Vectors stored in memory
   ↓
7. Processing logged to Redis
```

### 3. Soru-Cevap Süreci (RAG Pipeline)
```
1. User asks question via frontend
   ↓
2. .NET Core receives question
   ↓
3. Question embedded via Ollama
   ↓
4. Similar vectors searched in memory
   ↓
5. Top 5 relevant chunks retrieved
   ↓
6. Context built from chunks
   ↓
7. Context + question sent to Ollama LLM
   ↓
8. AI answer generated
   ↓
9. Response with sources sent to user
```

## 🐍 Python Worker Detayları

### Sorumluluklar
- **Dosya İşleme**: PDF ve Markdown dosyalarını parse etme
- **Text Chunking**: Büyük metinleri 1000 karakterlik parçalara bölme
- **Embedding Generation**: Ollama API ile vector oluşturma
- **Vector Storage**: In-memory vector database yönetimi
- **Redis Logging**: İşlem durumlarını loglama

### Dosya Yapısı
```
python-worker/
├── worker.py              # Ana worker script
├── document_processor.py  # PDF/MD parsing
├── vector_store.py        # Vector operations
├── requirements.txt       # Python dependencies
└── Dockerfile            # Container config
```

### Desteklenen Dosya Formatları
- **PDF**: PyMuPDF (fitz) + PyPDF2 fallback
- **Markdown**: Markdown parser ile HTML'e çevirip text extraction

### Chunking Stratejisi
- **Chunk Size**: 1000 karakter
- **Overlap**: 200 karakter
- **Strategy**: Kelime sınırında bölme
- **Fallback**: Zorla bölme (gerekirse)

## 🏗️ .NET Core Backend Detayları

### Sorumluluklar
- **File Upload API**: Dosya yükleme ve validasyon
- **Question Answering**: RAG pipeline yönetimi
- **Vector Search**: Similarity search operations
- **LLM Integration**: Ollama chat completion
- **Redis Management**: Queue ve logging
- **Frontend Serving**: Static file serving

### Proje Yapısı
```
backend-api/
├── Controllers/
│   ├── UploadController.cs    # File upload
│   ├── QuestionController.cs  # RAG operations
│   ├── HealthController.cs    # Health check
│   └── LogsController.cs      # Application logs
├── Services/
│   ├── IRedisService.cs       # Redis operations
│   ├── RedisService.cs        # Redis implementation
│   ├── IFileStorageService.cs # File storage
│   ├── FileStorageService.cs  # Local file storage
│   ├── IOllamaService.cs      # Ollama integration
│   ├── OllamaService.cs       # Ollama implementation
│   ├── IRagService.cs         # RAG pipeline
│   └── RagService.cs          # RAG implementation
├── Models/
│   ├── LogEntry.cs            # Log model
│   ├── UploadResponse.cs      # Upload response
│   └── QuestionModels.cs      # RAG models
└── Program.cs                 # Startup configuration
```

### Dependency Injection
```csharp
// Redis Service
builder.Services.AddSingleton<IRedisService, RedisService>();

// File Storage
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

// HTTP Clients
builder.Services.AddHttpClient<IOllamaService, OllamaService>();

// RAG Services
builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddScoped<IRagService, RagService>();
```

## 🎨 Frontend Detayları

### Özellikler
- **Modern UI**: Gradient design, responsive layout
- **Real-time Chat**: AJAX ile API communication
- **Loading States**: Spinner ve disabled states
- **Error Handling**: Kullanıcı dostu hata mesajları
- **Source Attribution**: Kaynak dosya ve chunk bilgileri
- **Turkish Language**: Türkçe arayüz

### Teknoloji
- **HTML5**: Semantic markup
- **CSS3**: Flexbox, gradients, animations
- **Vanilla JavaScript**: No frameworks, lightweight
- **Fetch API**: Modern HTTP requests

## 🔧 Konfigürasyon

### Environment Variables
```yaml
# .NET Core Backend
STORAGE_TYPE=FileStorage
REDIS_CONNECTION_STRING=redis:6379
OLLAMA_HOST=http://ollama:11434
EMBEDDING_MODEL=nomic-embed-text
CHAT_MODEL=llama3.1:8b

# Python Worker
REDIS_HOST=redis
REDIS_PORT=6379
OLLAMA_HOST=http://ollama:11434
EMBEDDING_MODEL=nomic-embed-text
CHUNK_SIZE=1000
CHUNK_OVERLAP=200
```

### Docker Compose Services
```yaml
services:
  redis:          # Message queue & logging
  rag-backend-api: # .NET Core API
  worker:         # Python vectorizer
  ollama:         # LLM & embeddings
  frontend:       # Web interface
```

## 📊 Monitoring & Logging

### Log Events
- **APP_STARTED**: Uygulama başlatıldı
- **REDIS_CONNECTED**: Redis bağlantısı kuruldu
- **FILE_UPLOADED**: Dosya yüklendi
- **FILE_QUEUED**: Dosya kuyruğa eklendi
- **FILE_PROCESSING_COMPLETED**: Dosya işlendi
- **FILE_PROCESSING_ERROR**: İşleme hatası
- **PYTHON_WORKER_STARTED**: Worker başlatıldı

### Health Monitoring
- **Uptime tracking**: Uygulama çalışma süresi
- **Redis connection**: Bağlantı durumu
- **Service status**: Tüm servislerin durumu
- **Vector stats**: Vector database istatistikleri

## 🚀 Performance & Scalability

### Optimizasyonlar
- **In-memory storage**: Hızlı vector access
- **Chunking strategy**: Optimal text processing
- **Async operations**: Non-blocking I/O
- **Connection pooling**: Efficient resource usage
- **Error handling**: Graceful degradation

### Scalability
- **Horizontal scaling**: Multiple Python workers
- **Load balancing**: Multiple .NET instances
- **Redis clustering**: Distributed queue
- **Ollama scaling**: Multiple model instances

## 🔒 Güvenlik

### File Security
- **File type validation**: Sadece PDF/MD
- **File size limits**: Boyut sınırlaması
- **Filename sanitization**: Güvenli dosya adları
- **Duplicate detection**: Hash-based checking

### API Security
- **Input validation**: Request validation
- **Error handling**: Güvenli hata mesajları
- **Rate limiting**: API rate limiting (gelecek)
- **CORS configuration**: Cross-origin setup

## 🐛 Troubleshooting

### Yaygın Sorunlar

#### 1. Ollama Model Bulunamadı
```bash
# Modelleri kontrol et
docker exec -it ollama_llm ollama list

# Model indir
docker exec -it ollama_llm ollama pull llama3.1:8b
docker exec -it ollama_llm ollama pull nomic-embed-text
```

#### 2. Redis Bağlantı Hatası
```bash
# Redis durumunu kontrol et
docker logs redis_cache

# Redis'i yeniden başlat
docker restart redis_cache
```

#### 3. Dosya İzin Hatası
```bash
# İzinleri ayarla
./setup-permissions.sh

# Docker volumes'u kontrol et
docker volume ls
```

#### 4. Memory Yetersizliği
```bash
# Memory kullanımını kontrol et
docker stats

# Ollama memory limit ayarla
docker compose down
# docker-compose.yml'de memory limit ekle
docker compose up
```

### Debug Komutları
```bash
# Tüm servislerin durumu
docker compose ps

# Log'ları takip et
docker compose logs -f

# Belirli servisin log'u
docker logs rag_worker --tail 50

# Redis içeriğini kontrol et
docker exec -it redis_cache redis-cli
> KEYS *
> LLEN application_logs
```

## 📈 Gelecek Geliştirmeler

### Planlanan Özellikler
- [ ] **Authentication**: User management
- [ ] **Rate Limiting**: API throttling
- [ ] **Persistent Storage**: Vector database
- [ ] **Multi-language**: Çoklu dil desteği
- [ ] **File Management**: Dosya silme/düzenleme
- [ ] **Export Features**: Sonuçları export etme
- [ ] **Analytics**: Kullanım istatistikleri
- [ ] **WebSocket**: Real-time updates

### Teknik İyileştirmeler
- [ ] **Caching**: Response caching
- [ ] **Compression**: Gzip compression
- [ ] **Monitoring**: Prometheus/Grafana
- [ ] **Testing**: Unit & integration tests
- [ ] **CI/CD**: Automated deployment
- [ ] **Documentation**: API documentation

## 📄 Lisans

Bu proje MIT lisansı altında lisanslanmıştır.

## 🤝 Katkıda Bulunma

1. Fork yapın
2. Feature branch oluşturun (`git checkout -b feature/amazing-feature`)
3. Commit yapın (`git commit -m 'Add amazing feature'`)
4. Push yapın (`git push origin feature/amazing-feature`)
5. Pull Request oluşturun

## 📞 İletişim

- **Proje**: Chat Your Docs with AI
- **Versiyon**: 1.0.0
- **Son Güncelleme**: 2024-12-20

---

**Not**: Bu sistem development ortamı için tasarlanmıştır. Production kullanımı için ek güvenlik ve performans optimizasyonları gerekebilir.
