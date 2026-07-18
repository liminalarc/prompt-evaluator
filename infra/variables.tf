variable "aws_region" {
  type    = string
  default = "us-east-1"
}

variable "github_org" {
  type    = string
  default = "liminalarc"
}

variable "github_repo" {
  type    = string
  default = "litmus-ai"
}

# Two images: the combined web+api app, and the Python eval-runner. One ECR repo each.
variable "ecr_repo_app" {
  type    = string
  default = "litmus-ai"
}

variable "ecr_repo_eval_runner" {
  type    = string
  default = "litmus-ai-eval-runner"
}

# Shared non-prod network. App Runner forbids two VPC connectors with the same subnet+SG combo, so
# LitmusAI reuses StormBoard's `stormboard-dev` connector (network plumbing only — no StormBoard
# resource is modified). The app service egresses through it to reach LitmusAI's own RDS Postgres,
# which we place in the same shared VPC. Mirrors Prism (Prism reuses the shared RDS too; LitmusAI is
# Postgres, so it brings its own DB into the shared VPC). Confirm the ARN still matches the connector.
variable "vpc_connector_arn" {
  type    = string
  default = "arn:aws:apprunner:us-east-1:973221168142:vpcconnector/stormboard-dev/1/d65aa6766cf949f583fc8ae27e96585e"
}

# Tag Name of the shared VPC + the App Runner connector's security group (from StormBoard's network
# module). Used to discover the VPC/subnets to host LitmusAI's RDS and to allow 5432 from the
# connector SG. Defaults match `stormboard-${env}` naming.
variable "shared_vpc_name" {
  type    = string
  default = "stormboard-dev"
}

variable "shared_connector_sg_name" {
  type    = string
  default = "stormboard-dev-app"
}

variable "db_instance_class" {
  type    = string
  default = "db.t4g.micro" # smallest current-gen; dev-sized
}

# Bootstrap admin seeded on first boot (Auth__BootstrapAdmin__*): Owner of the Default org + global
# admin. The password is generated (Secrets Manager); retrieve it via the AWS CLI after apply.
variable "admin_email" {
  type    = string
  default = "todd.hurley@liminalarc.co"
}

variable "admin_display_name" {
  type    = string
  default = "Todd Hurley"
}

# App Runner services need an image in ECR first, so they're created in a second apply
# (create_services=true) after the first images are pushed. See infra/README.md.
variable "create_services" {
  type    = bool
  default = false
}
