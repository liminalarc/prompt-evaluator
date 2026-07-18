output "ecr_repository_url_app" {
  description = "ECR repo for the combined web+api image"
  value       = aws_ecr_repository.app.repository_url
}

output "ecr_repository_url_eval_runner" {
  description = "ECR repo for the eval-runner image"
  value       = aws_ecr_repository.eval_runner.repository_url
}

output "github_deploy_role_arn" {
  description = "Hardcode as AWS_ROLE_ARN in the deploy workflow"
  value       = aws_iam_role.github_deploy.arn
}

output "db_endpoint" {
  description = "RDS Postgres endpoint (host:port)"
  value       = aws_db_instance.this.endpoint
}

output "app_service_url" {
  description = "App Runner URL of the app (once create_services=true)"
  value       = var.create_services ? aws_apprunner_service.app[0].service_url : null
}

output "eval_runner_service_url" {
  description = "App Runner URL of the eval-runner (once create_services=true)"
  value       = var.create_services ? aws_apprunner_service.eval_runner[0].service_url : null
}
