provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project   = "LitmusAI"
      ManagedBy = "Terraform"
      Tier      = "nonprod-incubation"
    }
  }
}

# ---------------------------------------------------------------------------
# Shared network (reused, never created) — StormBoard's `stormboard-dev` VPC.
# LitmusAI's App Runner app egresses through the shared VPC connector (var.vpc_connector_arn)
# and its RDS Postgres lives in this VPC's private subnets. No StormBoard resource is modified.
# ---------------------------------------------------------------------------
data "aws_vpc" "shared" {
  filter {
    name   = "tag:Name"
    values = [var.shared_vpc_name]
  }
}

data "aws_subnets" "shared_private" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.shared.id]
  }
  filter {
    name   = "tag:Tier"
    values = ["private"]
  }
}

# The SG attached to the shared App Runner VPC connector — our RDS admits Postgres traffic only
# from this SG.
data "aws_security_group" "shared_connector" {
  vpc_id = data.aws_vpc.shared.id
  filter {
    name   = "tag:Name"
    values = [var.shared_connector_sg_name]
  }
}

# ---------------------------------------------------------------------------
# ECR — one repo per image (combined app + eval-runner)
# ---------------------------------------------------------------------------
resource "aws_ecr_repository" "app" {
  name                 = var.ecr_repo_app
  image_tag_mutability = "MUTABLE"
  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "aws_ecr_repository" "eval_runner" {
  name                 = var.ecr_repo_eval_runner
  image_tag_mutability = "MUTABLE"
  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "aws_ecr_lifecycle_policy" "app" {
  repository = aws_ecr_repository.app.name
  policy     = local.ecr_lifecycle
}

resource "aws_ecr_lifecycle_policy" "eval_runner" {
  repository = aws_ecr_repository.eval_runner.name
  policy     = local.ecr_lifecycle
}

locals {
  ecr_lifecycle = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Keep last 15 images"
      selection    = { tagStatus = "any", countType = "imageCountMoreThan", countNumber = 15 }
      action       = { type = "expire" }
    }]
  })
}

# ---------------------------------------------------------------------------
# GitHub Actions OIDC deploy role (reuses the account-wide OIDC provider)
# ---------------------------------------------------------------------------
data "aws_iam_openid_connect_provider" "github" {
  url = "https://token.actions.githubusercontent.com"
}

data "aws_iam_policy_document" "github_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]
    principals {
      type        = "Federated"
      identifiers = [data.aws_iam_openid_connect_provider.github.arn]
    }
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }
    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values   = ["repo:${var.github_org}/${var.github_repo}:*"]
    }
  }
}

resource "aws_iam_role" "github_deploy" {
  name               = "litmus-github-deploy"
  description        = "Assumed by GitHub Actions (${var.github_org}/${var.github_repo}) to push images and deploy App Runner"
  assume_role_policy = data.aws_iam_policy_document.github_assume.json
}

data "aws_iam_policy_document" "github_deploy" {
  statement {
    sid       = "EcrAuth"
    effect    = "Allow"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }
  statement {
    sid    = "EcrPushPull"
    effect = "Allow"
    actions = [
      "ecr:BatchCheckLayerAvailability", "ecr:CompleteLayerUpload", "ecr:InitiateLayerUpload",
      "ecr:PutImage", "ecr:UploadLayerPart", "ecr:BatchGetImage", "ecr:GetDownloadUrlForLayer",
      "ecr:DescribeImages", "ecr:DescribeRepositories",
    ]
    resources = [aws_ecr_repository.app.arn, aws_ecr_repository.eval_runner.arn]
  }
  statement {
    sid    = "AppRunnerDeploy"
    effect = "Allow"
    actions = [
      "apprunner:StartDeployment", "apprunner:UpdateService", "apprunner:DescribeService",
      "apprunner:ListServices", "apprunner:ListOperations", "apprunner:DescribeOperation",
      "apprunner:ResumeService",
    ]
    resources = ["*"]
  }
  statement {
    sid       = "PassAppRunnerRoles"
    effect    = "Allow"
    actions   = ["iam:PassRole"]
    resources = ["arn:aws:iam::*:role/litmus-*"]
    condition {
      test     = "StringEquals"
      variable = "iam:PassedToService"
      values   = ["apprunner.amazonaws.com", "tasks.apprunner.amazonaws.com"]
    }
  }
}

resource "aws_iam_role_policy" "github_deploy" {
  name   = "litmus-github-deploy"
  role   = aws_iam_role.github_deploy.id
  policy = data.aws_iam_policy_document.github_deploy.json
}

# ---------------------------------------------------------------------------
# App Runner roles — access (ECR pull) + instance (reads its secrets)
# ---------------------------------------------------------------------------
resource "aws_iam_role" "apprunner_access" {
  name = "litmus-apprunner-access"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "build.apprunner.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

resource "aws_iam_role_policy_attachment" "apprunner_access_ecr" {
  role       = aws_iam_role.apprunner_access.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSAppRunnerServicePolicyForECRAccess"
}

resource "aws_iam_role" "apprunner_instance" {
  name = "litmus-apprunner-instance"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "tasks.apprunner.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

resource "aws_iam_role_policy" "apprunner_instance_secrets" {
  name = "read-litmus-secrets"
  role = aws_iam_role.apprunner_instance.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = ["secretsmanager:GetSecretValue"]
      Resource = [
        aws_secretsmanager_secret.db.arn,
        aws_secretsmanager_secret.service_token.arn,
        aws_secretsmanager_secret.anthropic.arn,
        aws_secretsmanager_secret.openai.arn,
        aws_secretsmanager_secret.bootstrap_admin.arn,
      ]
    }]
  })
}

# ---------------------------------------------------------------------------
# RDS Postgres — LitmusAI's own database (the shared RDS is SQL Server, unusable here)
# ---------------------------------------------------------------------------
resource "aws_security_group" "rds" {
  name        = "litmus-ai-rds"
  description = "LitmusAI RDS Postgres - accepts 5432 only from the shared App Runner connector SG"
  vpc_id      = data.aws_vpc.shared.id

  ingress {
    description     = "Postgres from the shared App Runner connector"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [data.aws_security_group.shared_connector.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "litmus-ai-rds" }
}

resource "aws_db_subnet_group" "this" {
  name       = "litmus-ai"
  subnet_ids = data.aws_subnets.shared_private.ids
  tags       = { Name = "litmus-ai" }
}

# Resolve a valid default Postgres version for the region rather than hardcoding one that may not
# exist (Stormboard's pattern). Bump deliberately if you want a specific minor.
data "aws_rds_engine_version" "postgres" {
  engine       = "postgres"
  default_only = true
}

# Restricted specials so the value is safe inside an Npgsql key-value connection string.
resource "random_password" "db" {
  length           = 24
  special          = true
  override_special = "!#%*-_"
}

resource "aws_db_instance" "this" {
  identifier     = "litmus-ai"
  engine         = "postgres"
  engine_version = data.aws_rds_engine_version.postgres.version
  instance_class = var.db_instance_class

  db_name  = "litmusai"
  username = "litmus"
  password = random_password.db.result

  allocated_storage     = 20
  max_allocated_storage = 100
  storage_type          = "gp3"
  storage_encrypted     = true

  db_subnet_group_name    = aws_db_subnet_group.this.name
  vpc_security_group_ids  = [aws_security_group.rds.id]
  publicly_accessible     = false
  multi_az                = false
  backup_retention_period = 1
  deletion_protection     = false
  skip_final_snapshot     = true
  apply_immediately       = true

  tags = { Name = "litmus-ai" }
}

locals {
  db_connection_string = "Host=${aws_db_instance.this.address};Port=${aws_db_instance.this.port};Database=litmusai;Username=litmus;Password=${random_password.db.result};SSL Mode=Require;Trust Server Certificate=true"
}

# ---------------------------------------------------------------------------
# Secrets — DB connection string, the internal service token, the Anthropic key
# ---------------------------------------------------------------------------
resource "aws_secretsmanager_secret" "db" {
  name = "litmus-ai/ConnectionStrings__Postgres"
}

resource "aws_secretsmanager_secret_version" "db" {
  secret_id     = aws_secretsmanager_secret.db.id
  secret_string = local.db_connection_string
}

# Shared internal service token: the app attaches it as X-Service-Token, the eval-runner requires it.
resource "random_password" "service_token" {
  length  = 48
  special = false # alphanumeric — safe as an HTTP header value
}

resource "aws_secretsmanager_secret" "service_token" {
  name = "litmus-ai/EvalRunner__ServiceToken"
}

resource "aws_secretsmanager_secret_version" "service_token" {
  secret_id     = aws_secretsmanager_secret.service_token.id
  secret_string = random_password.service_token.result
}

# Model-provider keys are set out-of-band via the AWS CLI (never committed / never in state as a real
# value) — placeholders here, real values via `aws secretsmanager put-secret-value`.
resource "aws_secretsmanager_secret" "anthropic" {
  name = "litmus-ai/ANTHROPIC_API_KEY"
}

resource "aws_secretsmanager_secret_version" "anthropic" {
  secret_id     = aws_secretsmanager_secret.anthropic.id
  secret_string = "PLACEHOLDER-set-via-cli"

  lifecycle {
    ignore_changes = [secret_string]
  }
}

resource "aws_secretsmanager_secret" "openai" {
  name = "litmus-ai/OPENAI_API_KEY"
}

resource "aws_secretsmanager_secret_version" "openai" {
  secret_id     = aws_secretsmanager_secret.openai.id
  secret_string = "PLACEHOLDER-set-via-cli"

  lifecycle {
    ignore_changes = [secret_string]
  }
}

# Bootstrap admin password — generated (real value in Secrets Manager, like Stormboard). min_* satisfy
# the Identity policy (>=8, upper/lower/digit). Retrieve: aws secretsmanager get-secret-value
# --secret-id litmus-ai/Auth__BootstrapAdmin__Password
resource "random_password" "bootstrap_admin" {
  length           = 24
  special          = true
  override_special = "!#%*-_"
  min_upper        = 2
  min_lower        = 2
  min_numeric      = 2
  min_special      = 1
}

resource "aws_secretsmanager_secret" "bootstrap_admin" {
  name = "litmus-ai/Auth__BootstrapAdmin__Password"
}

resource "aws_secretsmanager_secret_version" "bootstrap_admin" {
  secret_id     = aws_secretsmanager_secret.bootstrap_admin.id
  secret_string = random_password.bootstrap_admin.result
}

# ---------------------------------------------------------------------------
# App Runner services (created in a 2nd apply once the first images exist)
# ---------------------------------------------------------------------------

# eval-runner: Python service, public egress (reaches the Anthropic API). Protected by the shared
# service token, so it's safe to be internet-reachable at its App Runner URL.
resource "aws_apprunner_service" "eval_runner" {
  count        = var.create_services ? 1 : 0
  service_name = "litmus-ai-eval-runner"

  source_configuration {
    authentication_configuration {
      access_role_arn = aws_iam_role.apprunner_access.arn
    }
    image_repository {
      image_identifier      = "${aws_ecr_repository.eval_runner.repository_url}:dev"
      image_repository_type = "ECR"
      image_configuration {
        port = "8000"
        runtime_environment_secrets = {
          "ANTHROPIC_API_KEY"         = aws_secretsmanager_secret.anthropic.arn
          "OPENAI_API_KEY"            = aws_secretsmanager_secret.openai.arn
          "EVAL_RUNNER_SERVICE_TOKEN" = aws_secretsmanager_secret.service_token.arn
        }
      }
    }
    auto_deployments_enabled = false
  }

  instance_configuration {
    cpu               = "1024"
    memory            = "2048"
    instance_role_arn = aws_iam_role.apprunner_instance.arn
  }

  health_check_configuration {
    protocol            = "HTTP"
    path                = "/health"
    interval            = 10
    timeout             = 5
    healthy_threshold   = 1
    unhealthy_threshold = 5
  }

  depends_on = [
    aws_secretsmanager_secret_version.anthropic,
    aws_secretsmanager_secret_version.openai,
    aws_secretsmanager_secret_version.service_token,
  ]
}

# app: the combined web+api image. VPC egress (reaches RDS privately; the internet, incl. the
# eval-runner's public URL, via the shared NAT). The SPA is served same-origin by the API.
resource "aws_apprunner_service" "app" {
  count        = var.create_services ? 1 : 0
  service_name = "litmus-ai"

  source_configuration {
    authentication_configuration {
      access_role_arn = aws_iam_role.apprunner_access.arn
    }
    image_repository {
      image_identifier      = "${aws_ecr_repository.app.repository_url}:dev"
      image_repository_type = "ECR"
      image_configuration {
        port = "8080"
        runtime_environment_variables = {
          ASPNETCORE_ENVIRONMENT              = "Production"
          "EvalRunner__BaseUrl"               = "https://${aws_apprunner_service.eval_runner[0].service_url}"
          "Auth__BootstrapAdmin__Email"       = var.admin_email
          "Auth__BootstrapAdmin__DisplayName" = var.admin_display_name
        }
        runtime_environment_secrets = {
          "ConnectionStrings__Postgres"    = aws_secretsmanager_secret.db.arn
          "EvalRunner__ServiceToken"       = aws_secretsmanager_secret.service_token.arn
          "Auth__BootstrapAdmin__Password" = aws_secretsmanager_secret.bootstrap_admin.arn
        }
      }
    }
    auto_deployments_enabled = false
  }

  instance_configuration {
    cpu               = "1024"
    memory            = "2048"
    instance_role_arn = aws_iam_role.apprunner_instance.arn
  }

  network_configuration {
    egress_configuration {
      egress_type       = "VPC"
      vpc_connector_arn = var.vpc_connector_arn
    }
  }

  health_check_configuration {
    protocol            = "HTTP"
    path                = "/health"
    interval            = 10
    timeout             = 5
    healthy_threshold   = 1
    unhealthy_threshold = 5
  }

  depends_on = [
    aws_secretsmanager_secret_version.db,
    aws_secretsmanager_secret_version.service_token,
    aws_secretsmanager_secret_version.bootstrap_admin,
  ]
}
