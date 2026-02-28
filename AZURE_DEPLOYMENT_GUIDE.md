# VoteMaster - Azure Deployment Guide

This guide provides detailed step-by-step instructions for deploying VoteMaster to Azure App Service with Azure SQL Database, both using free tiers.

## Prerequisites

- Azure account (sign up at https://azure.microsoft.com/free/)
- GitHub account with your VoteMaster repository
- Basic understanding of Azure Portal navigation

## Overview

You'll be setting up:
- **Azure SQL Database** (Free tier - 32 GB storage)
- **Azure App Service** (F1 Free tier - 60 min/day compute)
- **GitHub Actions** (Automated CI/CD pipeline)

Total cost: **$0/month** (using free tiers)

---

## Part 1: Create Azure SQL Database

### Step 1: Access Azure Portal

1. Go to https://portal.azure.com
2. Sign in with your Azure account
3. You'll see the Azure Portal home page

### Step 2: Create SQL Database

1. Click **"SQL databases"** from the Azure services section (or search for it)
2. Click **"+ Create"** button at the top
3. Choose **"SQL Database (free offer)"** if available, otherwise choose "SQL Database"

### Step 3: Configure Database Basics

Fill in the following information:

**Subscription**: Your Azure subscription (auto-selected)

**Resource Group**: 
- Click "Create new"
- Name: `VoteMasterResourceGroup` or `VoteMaster-RG`
- Click "OK"

**Database Name**: `VoteMasterDb`

**Server**: Click "Create new"
- **Server name**: `votemaster-sql-[your-unique-number]` (e.g., `votemaster-sql-12345`)
  - Must be globally unique
  - Azure will show a checkmark if available
- **Location**: Choose closest to your users (e.g., "Southeast Asia", "East US")
- **Authentication method**: Select "Use SQL authentication"
- **Server admin login**: `sqladmin`
- **Password**: Create a strong password (e.g., `VoteMaster2024!`)
- **Confirm password**: Re-enter the same password
- **IMPORTANT**: Write down this password - you'll need it later!
- Click "OK"

**Want to use SQL elastic pool?**: No

**Workload environment**: Development

**Compute + storage**: 
- If you selected free offer, this is auto-configured
- Otherwise, click "Configure database"
  - Click "Looking for basic, standard, premium?"
  - Select "Basic" (2 GB, ~$5/month)
  - Click "Apply"

### Step 4: Review and Create

1. Click **"Review + create"** at the bottom
2. Review your settings
3. Click **"Create"**
4. Wait 2-3 minutes for deployment to complete
5. Click **"Go to resource"** when deployment completes

### Step 5: Configure SQL Server Firewall

1. You should now be on your SQL Server page
2. If not, click on the server name link (e.g., `votemaster-sql-12345.database.windows.net`)
3. In the left menu, under "Security", click **"Networking"**
4. Under "Public access" tab, find:
   - **"Allow Azure services and resources to access this server"**
5. Toggle it to **"Yes"** or check the box
6. Click **"Save"** at the top
7. Wait for "Successfully updated firewall rules" message

### Step 6: Get Database Connection String

1. In the left menu, click **"SQL databases"** (under Settings)
2. Click on your database: **"VoteMasterDb"**
3. In the left menu, click **"Connection strings"**
4. Copy the **"ADO.NET (SQL authentication)"** connection string
5. It looks like:
   ```
   Server=tcp:votemaster-sql-12345.database.windows.net,1433;Initial Catalog=VoteMasterDb;Persist Security Info=False;User ID=sqladmin;Password={your_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
   ```
6. **Replace `{your_password}` with your actual SQL admin password**
7. **Save this complete connection string in a notepad** - you'll need it soon!

---

## Part 2: Create Azure App Service

### Step 1: Create Web App

1. Click the Azure logo (top left) to go to home
2. Click **"Create a resource"** (the + icon)
3. Search for **"Web App"**
4. Click **"Create"** or **"Web App"** → **"Create"**

### Step 2: Configure Web App Basics

**Subscription**: Your Azure subscription

**Resource Group**: Select **"VoteMasterResourceGroup"** (same as database)

**Name**: `votemaster-app-[your-name]` (e.g., `votemaster-seizue`)
- Must be globally unique
- This becomes your URL: `https://votemaster-app-yourname.azurewebsites.net`

**Publish**: **Code**

**Runtime stack**: **.NET 9 (STS)** or **.NET 8 (LTS)** (whichever is available)

**Operating System**: **Windows** or **Linux** (either works, Linux is slightly cheaper)

**Region**: **Same as your database** (e.g., "Southeast Asia")

### Step 3: Configure Pricing Plan

**Pricing plan**: 
1. Click **"Explore pricing plans"** or **"Change size"**
2. Look for **"F1"** (Free tier)
   - 60 min/day compute time
   - 1 GB RAM
   - 1 GB storage
3. Select **"F1"**
4. Click **"Select"** or **"Apply"**

### Step 4: Review and Create

1. Click **"Review + create"** at the bottom
2. Review your settings
3. Click **"Create"**
4. Wait 1-2 minutes for deployment
5. Click **"Go to resource"** when complete

---

## Part 3: Configure App Service

### Step 1: Add Connection String

1. In your App Service, left menu → **"Environment variables"** (under Settings)
2. Click the **"Connection strings"** tab at the top
3. Click **"+ Add"** or **"New connection string"**
4. Fill in:
   - **Name**: `DefaultConnection`
   - **Value**: Paste your SQL connection string (from Part 1, Step 6)
   - **Type**: Select **"SQLAzure"**
   - **Deployment slot setting**: Leave unchecked
5. Click **"Apply"** or **"OK"**
6. Click **"Apply"** at the bottom of the page
7. Click **"Confirm"** when prompted (this restarts your app)

### Step 2: Enable Basic Authentication

1. In the left menu, click **"Configuration"** (under Settings)
2. Click the **"General settings"** tab
3. Scroll down to find **"SCM Basic Auth Publishing Credentials"**
4. Toggle it to **"On"**
5. Click **"Save"** at the top
6. Click **"Continue"** when prompted

### Step 3: Download Publish Profile

1. Go back to **"Overview"** (left menu)
2. In the top menu bar, click **"Download publish profile"**
3. A `.PublishSettings` file will download to your computer
4. Open this file with Notepad
5. **Copy ALL the content** (Ctrl+A, then Ctrl+C)
6. Keep this content - you'll need it for GitHub

### Step 4: Get Your App URL

1. In the **"Overview"** page, look for **"Default domain"**
2. This is your public URL (e.g., `https://votemaster-seizue.azurewebsites.net`)
3. Copy this URL - this is where your app will be accessible

---

## Part 4: Setup GitHub Actions Deployment

### Step 1: Add Publish Profile to GitHub Secrets

1. Go to your GitHub repository (e.g., `https://github.com/yourusername/VoteMaster`)
2. Click the **"Settings"** tab (top right)
3. In the left menu:
   - Click **"Secrets and variables"**
   - Click **"Actions"**
4. Click **"New repository secret"** (green button)
5. Fill in:
   - **Name**: `Azure_VoteMaster_Publish_Profile`
   - **Secret**: Paste the entire publish profile content from Part 3, Step 3
6. Click **"Add secret"**

### Step 2: Update GitHub Workflow File

The workflow file is already configured at `.github/workflows/dotnet.yml`. You just need to verify the app name:

1. In your GitHub repository, navigate to `.github/workflows/dotnet.yml`
2. Click the pencil icon to edit
3. Find this line near the bottom:
   ```yaml
   app-name: 'votemaster-seizue'
   ```
4. Change `votemaster-seizue` to your actual Azure App Service name
5. Verify the secret name is: `Azure_VoteMaster_Publish_Profile`
6. Click **"Commit changes"**
7. Click **"Commit changes"** again in the popup

### Step 3: Trigger Deployment

The deployment will automatically trigger when you commit to the `main` branch.

To watch the deployment:
1. Go to the **"Actions"** tab in your GitHub repository
2. You'll see the workflow running (yellow dot = in progress)
3. Click on the workflow to see detailed logs
4. Wait 3-5 minutes for completion
5. Green checkmark = successful deployment!

---

## Part 5: Verify Deployment

### Step 1: Enable Application Logging (Optional but Recommended)

1. In Azure Portal, go to your App Service
2. Left menu → **"App Service logs"** (under Monitoring)
3. Turn on:
   - **Application Logging (Filesystem)**: Set to **"Information"**
   - **Detailed Error Messages**: **On**
   - **Failed Request Tracing**: **On**
4. Click **"Save"**

### Step 2: Access Your Application

1. Open your browser
2. Go to your app URL: `https://your-app-name.azurewebsites.net`
3. **First load may take 10-20 seconds** (free tier wakes from sleep)
4. You should see the VoteMaster home page

### Step 3: Login and Test

1. Click "Login" or navigate to `/Account/Login`
2. Use default credentials:
   - Username: `admin`
   - Password: `admin123`
3. You should be able to access the admin dashboard
4. Test creating a poll and voting

---

## Troubleshooting

### App Shows HTTP 500 Error

**Check logs:**
1. Azure Portal → Your App Service → "Log stream"
2. Look for error messages

**Common causes:**
- Connection string not configured correctly
- SQL Server firewall not allowing Azure services
- Database migrations failed

**Solutions:**
1. Verify connection string in Environment variables
2. Check SQL Server networking settings
3. Restart the App Service (Overview → Restart)

### Database Connection Failed

**Check:**
1. Connection string has correct password (no `{your_password}` placeholder)
2. SQL Server firewall allows Azure services
3. Database exists and is online

**Test connection:**
1. Azure Portal → SQL Database → Query editor
2. Try logging in with your credentials
3. If successful, database is working

### GitHub Actions Deployment Failed

**Check:**
1. GitHub Actions logs for specific error
2. Verify `Azure_VoteMaster_Publish_Profile` secret is set correctly
3. Verify app name in workflow file matches Azure App Service name

**Re-download publish profile:**
1. Azure Portal → App Service → Download publish profile
2. Update GitHub secret with new content

### App is Slow or Times Out

**This is normal for free tier:**
- App sleeps after 20 minutes of inactivity
- First request takes 10-20 seconds to wake up
- Only 60 minutes/day of compute time

**Solutions:**
- Upgrade to Basic tier ($13/month) for always-on
- Accept the limitation for development/testing

---

## Free Tier Limitations

### App Service F1 (Free)
- 60 minutes/day compute time
- 1 GB RAM
- 1 GB disk storage
- App sleeps after 20 min inactivity
- No custom domains
- No SSL certificates
- Shared infrastructure

### SQL Database (Free)
- 32 GB storage
- 5 DTUs (limited performance)
- One free database per subscription
- Basic backup retention

### Recommendations
- Perfect for development, testing, and small projects
- For production with real users, consider upgrading to:
  - App Service: Basic B1 ($13/month) - always-on, custom domains
  - SQL Database: Basic ($5/month) or Standard ($15/month)

---

## Cost Optimization Tips

1. **Use serverless SQL** with auto-pause (pauses after 1 hour of inactivity)
2. **Delete resources** when not needed (can recreate later)
3. **Monitor usage** in Azure Cost Management dashboard
4. **Set spending limits** to avoid unexpected charges
5. **Use free tier** for development, upgrade only for production

---

## Security Best Practices

### After Deployment

1. **Change default admin password**
   - Login as admin
   - Go to user management
   - Update admin password

2. **Use Azure Key Vault** (optional, for production)
   - Store connection strings securely
   - Reference from App Service

3. **Enable HTTPS only**
   - App Service → Configuration → General settings
   - HTTPS Only: On

4. **Restrict SQL Server access**
   - Add specific IP addresses to firewall
   - Remove "Allow Azure services" if not needed

5. **Enable Application Insights** (optional)
   - Monitor performance and errors
   - Free tier available

---

## Updating Your Application

### Automatic Updates

Any push to the `main` branch automatically deploys:

```bash
git add .
git commit -m "Update feature"
git push origin main
```

GitHub Actions will:
1. Build the application
2. Run tests
3. Deploy to Azure
4. Your changes go live in 3-5 minutes

### Manual Deployment

If you need to deploy manually:

1. Build and publish locally:
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. Deploy using Azure CLI:
   ```bash
   az webapp deployment source config-zip --resource-group VoteMasterResourceGroup --name your-app-name --src publish.zip
   ```

---

## Monitoring and Maintenance

### View Logs
- **Real-time**: App Service → Log stream
- **Historical**: App Service → Diagnose and solve problems

### Monitor Performance
- App Service → Metrics
- View CPU, memory, response times

### Database Monitoring
- SQL Database → Query Performance Insight
- Monitor slow queries and resource usage

### Deployment History
- GitHub → Actions tab
- See all deployments and their status

---

## Next Steps

1. **Customize your application**
   - Update branding and styling
   - Add more features
   - Configure email notifications

2. **Add custom domain** (requires Basic tier)
   - Purchase domain
   - Configure DNS
   - Add to App Service

3. **Set up staging environment**
   - Create deployment slots
   - Test before production

4. **Implement monitoring**
   - Application Insights
   - Custom alerts
   - Performance tracking

5. **Scale as needed**
   - Upgrade App Service tier
   - Scale SQL Database
   - Add CDN for static files

---

## Support and Resources

- **Azure Documentation**: https://docs.microsoft.com/azure
- **ASP.NET Core Docs**: https://docs.microsoft.com/aspnet/core
- **GitHub Actions**: https://docs.github.com/actions
- **Azure Free Account**: https://azure.microsoft.com/free/

## Conclusion

You now have a fully deployed VoteMaster application running on Azure with:
- ✅ Free SQL Database
- ✅ Free App Service hosting
- ✅ Automated CI/CD pipeline
- ✅ Public internet access
- ✅ Scalable architecture

Your application is accessible at: `https://your-app-name.azurewebsites.net`

Enjoy your cloud-hosted weighted voting system!
