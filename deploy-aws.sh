#!/usr/bin/env bash
# ==============================================================================
# SAT Jewels - Automated 1-Click AWS Deployment Script
# ==============================================================================
# Deploys containerized .NET 8 application to AWS Elastic Container Registry (ECR)
# and triggers AWS App Runner / ECS for instantaneous zero-downtime deployment.
# ==============================================================================

set -euo pipefail

# Configuration defaults
AWS_REGION="${AWS_REGION:-us-east-1}"
APP_NAME="satjewels"
IMAGE_TAG="$(date +%Y%m%d%H%M%S)"

echo "💎 [SAT Jewels] Initializing AWS Deployment Preparation..."
echo "📍 Target Region: $AWS_REGION"

# 1. Verify AWS CLI Authentication
echo "🔑 Verifying AWS Credentials..."
if ! AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query "Account" --output text 2>/dev/null); then
  echo "❌ Error: AWS CLI is not logged in or credentials have expired."
  echo "   Please run 'aws configure' with your AWS Access Key & Secret first."
  exit 1
fi
echo "✅ Authenticated as AWS Account: $AWS_ACCOUNT_ID"

# 2. Ensure AWS ECR Repository Exists
ECR_URI="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/$APP_NAME"
echo "📦 Checking ECR Repository '$APP_NAME'..."
if ! aws ecr describe-repositories --repository-names "$APP_NAME" --region "$AWS_REGION" >/dev/null 2>&1; then
  echo "   Creating new ECR repository '$APP_NAME'..."
  aws ecr create-repository \
    --repository-name "$APP_NAME" \
    --region "$AWS_REGION" \
    --image-scanning-configuration scanOnPush=true >/dev/null
  echo "✅ ECR Repository created."
else
  echo "✅ ECR Repository exists."
fi

# 3. Log in Docker to AWS ECR
echo "🔐 Logging in Docker to AWS ECR..."
aws ecr get-login-password --region "$AWS_REGION" | docker login --username AWS --password-stdin "$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com"

# 4. Build Optimized Production Docker Image
echo "🔨 Building Production Docker image..."
docker build -t "$APP_NAME:latest" -t "$ECR_URI:latest" -t "$ECR_URI:$IMAGE_TAG" .

# 5. Push Image to ECR
echo "🚀 Pushing image to AWS ECR ($ECR_URI)..."
docker push "$ECR_URI:latest"
docker push "$ECR_URI:$IMAGE_TAG"

echo "✅ Image successfully pushed to ECR: $ECR_URI:latest"

# 6. Check for AWS App Runner Service to auto-deploy
EXISTING_SERVICE_ARN=$(aws apprunner list-services --region "$AWS_REGION" --query "ServiceSummaryList[?ServiceName=='$APP_NAME'].ServiceArn" --output text 2>/dev/null || echo "")

if [ -n "$EXISTING_SERVICE_ARN" ] && [ "$EXISTING_SERVICE_ARN" != "None" ]; then
  echo "🔄 Triggering instant update on AWS App Runner service..."
  aws apprunner start-deployment --service-arn "$EXISTING_SERVICE_ARN" --region "$AWS_REGION"
  echo "🎉 Deployment successfully triggered! Live update in progress on AWS App Runner."
else
  echo ""
  echo "=============================================================================="
  echo "🎉 Docker image is ready in AWS ECR!"
  echo "   ECR Image URI: $ECR_URI:latest"
  echo "=============================================================================="
  echo "To launch your service on AWS App Runner (with automatic HTTPS & domain support):"
  echo ""
  echo "aws apprunner create-service \\"
  echo "  --service-name $APP_NAME \\"
  echo "  --source-configuration '{\"ImageRepository\": {\"ImageIdentifier\": \"$ECR_URI:latest\", \"ImageRepositoryType\": \"ECR\", \"ImageConfiguration\": {\"Port\": \"8080\"}}}' \\"
  echo "  --region $AWS_REGION"
  echo "=============================================================================="
fi
