terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.9"
    }
  }

  # LitmusAI owns its own state, independent of StormBoard / Prism. The bucket is created once by
  # hand before `terraform init` (see infra/README.md) — Terraform can't bootstrap its own backend.
  backend "s3" {
    bucket       = "litmus-ai-tfstate-973221168142"
    key          = "litmus-ai/terraform.tfstate"
    region       = "us-east-1"
    encrypt      = true
    use_lockfile = true
  }
}
