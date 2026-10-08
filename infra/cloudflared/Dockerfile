# Imagem do conector do Cloudflare Tunnel c4c-billing-webhook.
# O ingress é copiado para dentro da imagem (sem bind mount), para o Coolify construir a partir do repositório.
FROM cloudflare/cloudflared:2026.10.0
COPY billing-webhook.yml /etc/cloudflared/billing-webhook.yml
