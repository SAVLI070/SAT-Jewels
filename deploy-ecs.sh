#!/usr/bin/env bash
# ==============================================================================
# SAT Jewels - Automated AWS ECS (Fargate) Build & Deployment Script
# ==============================================================================
set -euo pipefail

AWS_REGION="${AWS_REGION:-us-east-1}"
APP_NAME="sat-jewels-web"
CLUSTER_NAME="sat-jewels-cluster"
SERVICE_NAME="sat-jewels-service"

echo "💎 [SAT Jewels] Starting ECS Deployment Pipeline..."
echo "📍 Region: $AWS_REGION"

# 1. AWS Credentials
echo "🔑 Verifying AWS CLI authentication..."
AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query "Account" --output text)
echo "✅ Authenticated as Account: $AWS_ACCOUNT_ID"

ECR_URI="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/$APP_NAME"
VERSION_TAG="v$(date +%Y%m%d%H%M)"

# 2. Docker Login to ECR
echo "🔐 Logging into Amazon ECR..."
aws ecr get-login-password --region "$AWS_REGION" | docker login --username AWS --password-stdin "$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com"

# 3. Build Production Container
echo "🏗️ Building Docker container (linux/amd64)..."
docker build --platform linux/amd64 \
  -t "$APP_NAME:latest" \
  -t "$ECR_URI:latest" \
  -t "$ECR_URI:$VERSION_TAG" .

# 4. Push to ECR
echo "🚀 Pushing image tags to Amazon ECR..."
docker push "$ECR_URI:$VERSION_TAG"
docker push "$ECR_URI:latest"
echo "✅ Successfully pushed $ECR_URI:latest and $ECR_URI:$VERSION_TAG"

# 5. Register/Update Task Definition
echo "📝 Registering latest ECS Task Definition..."
TASK_DEF_ARN=$(aws ecs register-task-definition \
  --cli-input-json file://ecs-task-def.json \
  --region "$AWS_REGION" \
  --query "taskDefinition.taskDefinitionArn" --output text)
echo "✅ Registered Task Definition: $TASK_DEF_ARN"

# 6. Check/Update ECS Service
echo "🔄 Checking ECS Service '$SERVICE_NAME' in cluster '$CLUSTER_NAME'..."
EXISTING_SERVICE=$(aws ecs describe-services \
  --cluster "$CLUSTER_NAME" \
  --services "$SERVICE_NAME" \
  --region "$AWS_REGION" \
  --query "services[?status=='ACTIVE'].serviceName" \
  --output text 2>/dev/null || echo "")

if [ -n "$EXISTING_SERVICE" ] && [ "$EXISTING_SERVICE" != "None" ]; then
  echo "🚀 Updating ECS Service with latest image deployment..."
  aws ecs update-service \
    --cluster "$CLUSTER_NAME" \
    --service "$SERVICE_NAME" \
    --task-definition "$TASK_DEF_ARN" \
    --force-new-deployment \
    --region "$AWS_REGION" >/dev/null
  echo "🎉 ECS service deployment initiated!"
else
  echo "ℹ️ ECS cluster/service '$SERVICE_NAME' is not running yet."
  echo "   The latest Docker image and Task Definition ($TASK_DEF_ARN) are ready in AWS ECS."
  echo "   You can launch the service in the AWS ECS Console or via AWS CLI."
fi

echo ""
echo "=============================================================================="
echo "🎉 Latest SAT Jewels container successfully published to Amazon ECS!"
echo "   Image: $ECR_URI:latest"
echo "   Tag:   $VERSION_TAG"
echo "   Task:  $TASK_DEF_ARN"
echo "=============================================================================="
