#!/usr/bin/env bash
# ==============================================================================
# SAT Jewels - Automated 1-Click AWS App Runner Deployment Script
# ==============================================================================
set -euo pipefail

AWS_REGION="${AWS_REGION:-us-east-1}"
APP_NAME="sat-jewels-web"
DOMAIN="satjewel.com"

echo "💎 [SAT Jewels] Initializing AWS Deployment..."
echo "📍 Target Region: $AWS_REGION"

# 1. Verify AWS CLI Authentication
echo "🔑 Verifying AWS Credentials..."
if ! AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query "Account" --output text 2>/dev/null); then
  echo "❌ Error: AWS CLI is not authenticated or credentials have expired."
  exit 1
fi
echo "✅ Authenticated as AWS Account: $AWS_ACCOUNT_ID"

ECR_URI="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/$APP_NAME"

# 2. Check for Existing App Runner Service
echo "🔍 Checking for App Runner service '$APP_NAME'..."
EXISTING_SERVICE_ARN=$(aws apprunner list-services --region "$AWS_REGION" \
  --query "ServiceSummaryList[?ServiceName=='$APP_NAME'].ServiceArn" \
  --output text 2>/dev/null || echo "")

if [ -n "$EXISTING_SERVICE_ARN" ] && [ "$EXISTING_SERVICE_ARN" != "None" ]; then
  echo "🔄 Service exists ($EXISTING_SERVICE_ARN). Triggering new deployment..."
  aws apprunner start-deployment --service-arn "$EXISTING_SERVICE_ARN" --region "$AWS_REGION"
  echo "🎉 Deployment triggered! View progress in AWS App Runner Console."
else
  echo "🚀 Creating new App Runner service '$APP_NAME'..."
  aws apprunner create-service \
    --cli-input-json file://apprunner-service.json \
    --region "$AWS_REGION"
  echo "✅ App Runner service creation initiated!"
fi

echo ""
echo "=============================================================================="
echo "🎉 AWS App Runner is deploying the latest Docker image ($ECR_URI:latest)!"
echo "   Once the service status is 'RUNNING', run:"
echo "   ./link-domain.sh"
echo "   to automatically bind $DOMAIN and generate your DNS CNAME records."
echo "=============================================================================="
