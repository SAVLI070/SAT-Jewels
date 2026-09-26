# SAT Jewels - AWS App Runner Deployment Guide (`satjewel.com`)

## Status Summary

| Component | Status | Details |
| :--- | :--- | :--- |
| **Clean Build** | ✅ Done | Release mode, 0 errors, 0 warnings |
| **Media Optimization** | ✅ Done | Reduced from 5.8 GB to 56 MB; all product images on Cloudinary CDN |
| **PostgreSQL Database** | ✅ Verified | AWS RDS (`satjewels-postgres...`), port 5432 open, 25 tables, 339 products |
| **Docker Production Image** | ✅ Pushed | `105508555271.dkr.ecr.us-east-1.amazonaws.com/sat-jewels-web:latest` |
| **IAM Access Role** | ✅ Configured | `arn:aws:iam::105508555271:role/AppRunnerECRAccessRole` |
| **Service Spec & Scripts** | ✅ Ready | `apprunner-service.json`, `deploy-aws.sh`, `link-domain.sh` |

---

## Action Required Tomorrow (AWS Subscription / Activation)

When you activate the App Runner subscription on your AWS account tomorrow:

### Option A: 1-Command Automated Deployment (Terminal)

Open terminal in the project directory and run:

```bash
./deploy-aws.sh
```

This will automatically create the App Runner service using the pre-pushed container and the verified RDS database.

Once the service status shows `RUNNING` (takes ~3 minutes):
```bash
./link-domain.sh
```
This binds `satjewel.com` & `www.satjewel.com` and outputs the DNS CNAME records.

---

### Option B: Via AWS Management Console (GUI)

If you prefer using the AWS Console:

1. Go to **AWS App Runner** in region `us-east-1`:
   [https://console.aws.amazon.com/apprunner/home?region=us-east-1](https://console.aws.amazon.com/apprunner/home?region=us-east-1)
2. Click **Create service**.
3. **Source:**
   - Repository type: **Container registry**
   - Provider: **Amazon ECR**
   - Container image URI: Click **Browse** and select `sat-jewels-web` > `latest`
   - Deployment trigger: **Automatic**
   - ECR access role: Select existing `AppRunnerECRAccessRole`
4. **Configure service:**
   - Service name: `sat-jewels-web`
   - Virtual CPU: `1 vCPU`
   - Memory: `2 GB`
   - Port: `8080`
   - Health check path: `/`
5. Click **Create & deploy**.

---

## Custom Domain Setup (`satjewel.com`)

1. In AWS App Runner, go to **Custom domains** > **Link domain**.
2. Enter: `satjewel.com` (leave "enable www subdomain" checked).
3. App Runner will generate 2 CNAME records (one for certificate validation, one for routing).
4. Log into your domain registrar (GoDaddy, Namecheap, Cloudflare, etc.) and add the CNAME records.
5. AWS will automatically issue and renew a free SSL certificate (`https://satjewel.com`).
