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
    "Weight": 3
  },
  "Users": [
    { "Username": "bob", "Password": "pass123", "Weight": 1, "Role": "Voter" }
  ]
}
```
- You can add or remove default Users in the Users array.
- Once logged in as Admin, you can also add multiple voters directly through the system.

