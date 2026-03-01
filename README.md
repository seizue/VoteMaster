# VoteMaster
VoteMaster is a decision-making platform that implements a Weighted Voting System using ASP.NET Core MVC Razor Views, REST API, and Microsoft SQL Server.

In this system, each member’s vote carries weight based on their ownership or stake (e.g., number of shares). 
For example:
- A shareholder with 70M shares has more voting influence than one with 10M shares.

## Key Features
- Weighted Voting System – votes are proportional to shares or assigned weights.
- RESTful API – provides endpoints for managing members, votes, and results.
- Microsoft SQL Server – ensures reliable data storage and querying.
- Voting Results – displays final tallies and resolution outcomes based on weighted votes.

## Live Demo

The application is deployed on Azure and accessible at:
**https://votemaster-seizue.azurewebsites.net**

Default login credentials:
- Admin: Username `admin` / Password `admin123`
- Test Voter: Username `bob` / Password `pass123`

## Local Development

1. **Clone this repository**  
   ```bash
   git clone https://github.com/seizue/VoteMaster.git
   cd VoteMaster
2. **Open a terminal in the project folder and run:**
   ```bash
   dotnet build
   dotnet run
3. **Frontend: `http://localhost:5000`**

## Configuration

Default settings are stored in `appsettings.json`:
- Admin account credentials
- Test voter accounts (e.g., bob/pass123)
- Database connection string

You can add or modify default users in the `Seed.Users` array in `appsettings.json`. Once logged in as admin, you can also manage users directly through the system.

For detailed configuration options and deployment-specific settings, see [Docs.md](Docs.md).

## Deployment

VoteMaster can be deployed to multiple platforms:
- Azure App Service (with Azure SQL Database)
- AWS Elastic Beanstalk (with RDS)
- Docker containers
- Traditional IIS hosting
- Linux servers with Nginx

For complete deployment instructions including:
- Step-by-step setup guides
- Database configuration
- CI/CD pipeline setup
- Security best practices
- Troubleshooting tips

See the comprehensive [Deployment Guide (Docs.md)](Docs.md).

