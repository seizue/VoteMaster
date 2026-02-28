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
- Username: `admin`
- Password: `admin123`

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
All default settings (Admin account, Test voters, and database connection) are stored in `appsettings.json.`
You can modify these values to fit your environment. 
 ```csharp
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=VoteMasterDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
},
"Seed": {
  "Admin": {
    "Username": "admin",
    "Password": "admin123",
    "Weight": 1
  },
  "Users": [
    { "Username": "bob", "Password": "pass123", "Weight": 1, "Role": "Voter" }
  ]
}
```
- You can add or remove default Users in the Users array.
- Once logged in as Admin, you can also add multiple voters directly through the system.

## Azure Deployment

This application is configured for automatic deployment to Azure App Service using GitHub Actions.

### Deployment Architecture
- **Hosting**: Azure App Service (Free F1 tier)
- **Database**: Azure SQL Database (Free tier)
- **CI/CD**: GitHub Actions workflow
- **Region**: Southeast Asia

### Automatic Deployment
Every push to the `main` branch triggers an automatic deployment:
1. GitHub Actions builds the application
2. Runs tests (if available)
3. Publishes the app
4. Deploys to Azure App Service

### Manual Deployment Setup
For detailed step-by-step instructions on setting up Azure deployment from scratch, see [AZURE_DEPLOYMENT_GUIDE.md](AZURE_DEPLOYMENT_GUIDE.md).

Quick overview:
1. Create Azure SQL Database (free tier)
2. Create Azure App Service (F1 free tier)
3. Configure connection string in App Service
4. Add publish profile to GitHub Secrets
5. Push to main branch to deploy

### Environment Configuration
Production settings are managed through:
- **Connection String**: Configured in Azure App Service → Environment variables
- **App Settings**: `appsettings.Production.json`
- **GitHub Secrets**: `Azure_VoteMaster_Publish_Profile` (contains Azure publish profile for deployment)
- **GitHub Workflow**: `.github/workflows/dotnet.yml` (CI/CD pipeline configuration)

### Monitoring
- View logs: Azure Portal → App Service → Log stream
- Deployment history: GitHub → Actions tab
- App insights: Azure Portal → App Service → Monitoring

