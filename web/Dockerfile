# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/windows/servercore/iis:windowsservercore-ltsc2022

# El contenido real se monta por volumen (ver docker-compose.yml),
# pero copiamos el contenido inicial para que la imagen funcione
# también sin el volumen montado (por ejemplo en un "docker run" suelto).
COPY wwwroot/ C:/inetpub/wwwroot/

EXPOSE 80
