#!/bin/bash

# Data dizinlerini oluştur ve güvenli izinleri ayarla
echo "Data dizinleri oluşturuluyor ve güvenli izinleri ayarlanıyor..."

# Data dizinlerini oluştur
mkdir -p ./data/uploaded_files
mkdir -p ./data/vectors

# Container'da kullanılan user/group ID'leri (Dockerfile'dan)
CONTAINER_USER_ID=1000
CONTAINER_GROUP_ID=1000

# Host sistemde aynı ID'leri kullan (güvenlik için)
# Host sistemdeki mevcut user ID ile uyumlu
echo "Container user/group ID'leri: $CONTAINER_USER_ID:$CONTAINER_GROUP_ID"
echo "Host sistem user ID: $(id -u):$(id -g)"

# İzinleri ayarla (755 = rwxr-xr-x - güvenli)
chmod 755 ./data
chmod 755 ./data/uploaded_files
chmod 755 ./data/vectors

# Dizin sahipliğini kontrol et
echo "Dizin izinleri:"
ls -la ./data/

echo ""
echo "Güvenlik notu:"
echo "- 755 izinleri kullanılıyor (777 güvenlik riski)"
echo "- Container'da dotnetuser (UID:1000) dosya oluşturabilir"
echo "- Host sistemde volume mount user ID uyumlu (1000:1000)"
echo ""
echo "Setup tamamlandı!"
echo "Artık docker-compose up --build backend komutunu çalıştırabilirsiniz."
