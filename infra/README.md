# LitmusAI Infrastructure (AWS)

Terraform-managed **dev** deployment (spec 3.2), modeled on Prism: AWS App Runner + ECR + RDS,
reusing the shared `liminalarc` account's network. **Dev-only — there is no prod target yet.**

## What this creates vs reuses

**Reuses (shared, never modified):**
- AWS account **973221168142**, region **us-east-1**
- The account-wide **GitHub OIDC provider**
- StormBoard's **`stormboard-dev` VPC + VPC connector** — the app egresses through it (App Runner
  forbids a second connector on the same subnet+SG), and LitmusAI's RDS lives in this VPC.

**Creates (LitmusAI owns):**
- Two **ECR** repos: `litmus-ai` (combined web+api image) and `litmus-ai-eval-runner`
- **`litmus-github-deploy`** OIDC role (push images + deploy App Runner)
- App Runner **access** + **instance** roles
- Its own **RDS Postgres** (`db.t4g.micro`) in the shared VPC's private subnets, SG admits 5432
  only from the shared connector SG (the shared RDS is SQL Server — unusable for Postgres)
- **Secrets**: `litmus-ai/ConnectionStrings__Postgres`, `litmus-ai/EvalRunner__ServiceToken`
  (generated), `litmus-ai/ANTHROPIC_API_KEY` (placeholder — set out-of-band)
- Two **App Runner** services: `litmus-ai` (VPC egress → Postgres) and `litmus-ai-eval-runner`
  (public egress → Anthropic; protected by the service token)

## Prerequisites

- **AWS CLI v2** and **Terraform >= 1.10**
- AWS access via SSO (reuse the shared session):
  ```
  aws configure sso   # session: liminalarc, start URL: https://leadingagile.awsapps.com/start
                      # account: 973221168142, role: AdministratorAccess, region: us-east-1, profile: litmus
  aws sso login --profile litmus
  export AWS_PROFILE=litmus        # PowerShell: $env:AWS_PROFILE="litmus"
  ```

## State

Remote state in S3 bucket **`litmus-ai-tfstate-973221168142`** (versioned, encrypted), key
`litmus-ai/terraform.tfstate`, native S3 lockfile (no DynamoDB). Terraform can't create its own
backend, so make the bucket once:

```bash
aws s3api create-bucket --bucket litmus-ai-tfstate-973221168142 --region us-east-1
aws s3api put-bucket-versioning --bucket litmus-ai-tfstate-973221168142 \
  --versioning-configuration Status=Enabled
aws s3api put-public-access-block --bucket litmus-ai-tfstate-973221168142 \
  --public-access-block-configuration BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true
```

## Apply — two phases (App Runner needs an image first)

**Phase 1 — infra without the services** (ECR, roles, RDS, secrets):

```bash
terraform init
terraform apply                      # create_services defaults to false
```

Set the real Anthropic key (never in Terraform/state):

```bash
aws secretsmanager put-secret-value --secret-id litmus-ai/ANTHROPIC_API_KEY \
  --secret-string "sk-ant-…"
```

**Push the first images.** Either push to `main` (CI builds + pushes both `:dev` images — see
`.github/workflows/ci.yml`), or build/push by hand:

```bash
ACCT=973221168142.dkr.ecr.us-east-1.amazonaws.com
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin $ACCT
docker build -f deploy/Dockerfile -t $ACCT/litmus-ai:dev .
docker build -f eval-runner/Dockerfile -t $ACCT/litmus-ai-eval-runner:dev ./eval-runner
docker push $ACCT/litmus-ai:dev
docker push $ACCT/litmus-ai-eval-runner:dev
```

**Phase 2 — create the App Runner services** (now that images exist):

```bash
terraform apply -var="create_services=true"
terraform output app_service_url      # the reachable dev URL
```

## Verify

```bash
URL=$(terraform output -raw app_service_url)
curl -sf https://$URL/health
curl -sf https://$URL/api/version     # shape: {version, commit, buildTime, environment, channel}
```

## Notes

- **Confirm `vpc_connector_arn`** still matches `stormboard-dev`'s connector before applying (it's a
  var default). If StormBoard recreated its connector, update the default.
- Ongoing deploys are handled by CI (`start-deployment` on push to `main`); you only re-run
  `terraform apply` when the infra itself changes.
- Migrations run on app startup (`db.Database.Migrate()`), so the schema is created on first boot
  against the fresh `litmusai` database.
