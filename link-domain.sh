#!/usr/bin/env bash
# ==============================================================================
# SAT Jewels - Custom Domain Association Script for satjewel.com
# ==============================================================================
set -euo pipefail

AWS_REGION="${AWS_REGION:-us-east-1}"
SERVICE_NAME="sat-jewels-web"
DOMAIN="satjewel.com"

echo "💎 [SAT Jewels] Associating Custom Domain: $DOMAIN"

# 1. Fetch Service ARN
SERVICE_ARN=$(aws apprunner list-services --region "$AWS_REGION" \
  --query "ServiceSummaryList[?ServiceName=='$SERVICE_NAME'].ServiceArn" \
  --output text 2>/dev/null || echo "")

if [ -z "$SERVICE_ARN" ] || [ "$SERVICE_ARN" = "None" ]; then
  echo "❌ Error: App Runner service '$SERVICE_NAME' was not found in $AWS_REGION."
  echo "   Please run './deploy-aws.sh' first to create the service."
  exit 1
fi

echo "✅ Found Service: $SERVICE_ARN"

# 2. Associate Custom Domain
echo "🔗 Associating $DOMAIN (with www.$DOMAIN)..."
aws apprunner associate-custom-domain \
  --service-arn "$SERVICE_ARN" \
  --domain-name "$DOMAIN" \
  --enable-www-subdomain \
  --region "$AWS_REGION" || true

# 3. Retrieve Certificate & DNS Records
echo ""
echo "📋 Fetching required DNS validation records..."
aws apprunner describe-custom-domains \
  --service-arn "$SERVICE_ARN" \
  --region "$AWS_REGION" \
  --output table

echo ""
echo "=============================================================================="
echo "🎉 Next Step: Add the CNAME records shown above in your Domain Registrar DNS"
echo "   (GoDaddy, Namecheap, Cloudflare, etc.)."
echo "   Once added, AWS App Runner will automatically issue free SSL certificates"
echo "   and route https://satjewel.com and https://www.satjewel.com to your store!"
echo "=============================================================================="
