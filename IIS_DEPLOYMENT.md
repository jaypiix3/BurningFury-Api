# IIS Deployment Guide for BurningFury API

## Prerequisites

1. **IIS with ASP.NET Core Module**
   - Install IIS
   - Install ASP.NET Core Runtime 9.0 (hosting bundle)
   - Ensure ASP.NET Core Module v2 is installed

2. **.NET 9 Runtime**
   - Install .NET 9 Runtime on the server
   - Verify installation: `dotnet --list-runtimes`

## Deployment Steps

### 1. Publish the Application

```bash
# Navigate to the project directory
cd BurningFuryApi

# Publish for release
dotnet publish -c Release -o ./publish

# Or publish with specific runtime
dotnet publish -c Release -r win-x64 --self-contained false -o ./publish
```

### 2. Copy Files to IIS

1. Copy all files from the `./publish` folder to your IIS website directory (e.g., `C:\inetpub\wwwroot\BurningFuryApi`)
2. Ensure the `web.config` file is included in the deployment

### 3. IIS Configuration

#### Create Application Pool
1. Open IIS Manager
2. Create a new Application Pool:
   - **Name**: BurningFuryApi
   - **.NET CLR Version**: No Managed Code
   - **Managed Pipeline Mode**: Integrated
   - **Identity**: ApplicationPoolIdentity (or custom account with appropriate permissions)

#### Create Website/Application
1. Create a new website or application in IIS
2. Set the Application Pool to "BurningFuryApi"
3. Set the Physical Path to your deployment directory

### 4. Permissions

Ensure the Application Pool Identity has permissions to:
- Read/Execute on the application directory
- Write permissions to the `logs` folder (create if it doesn't exist)
- Access to the database connection

### 5. Troubleshooting Setup

#### Enable Detailed Logging

1. **Create logs directory** in your application folder:
   ```
   mkdir C:\inetpub\wwwroot\BurningFuryApi\logs
   ```

2. **Grant write permissions** to the Application Pool Identity for the logs folder

3. **Check web.config** settings:
   ```xml
   <aspNetCore processPath="dotnet" 
               arguments=".\BurningFuryApi.dll" 
               stdoutLogEnabled="true" 
               stdoutLogFile=".\logs\stdout" 
               hostingModel="OutOfProcess">
   ```

#### Check Event Logs
- Windows Event Viewer ? Windows Logs ? Application
- Look for errors from "IIS AspNetCore Module" or "IIS AspNetCore Module V2"

#### Test Database Connection
1. Navigate to: `https://yoursite.com/api/auth/health`
2. Check if database shows as "Connected"

## Configuration Files

### Environment Variables (Optional)
You can override appsettings in web.config:

```xml
<environmentVariables>
  <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
  <environmentVariable name="ConnectionStrings__DefaultConnection" value="your-connection-string" />
  <environmentVariable name="Auth0__Domain" value="your-auth0-domain.auth0.com" />
  <environmentVariable name="Auth0__Audience" value="your-audience" />
</environmentVariables>
```

### appsettings.Production.json
Ensure your production settings are correct:
- Database connection string
- Auth0 configuration
- Logging levels

## Common Issues and Solutions

### 1. HTTP Error 502.5 - ANCM Out-Of-Process Startup Failure

**Causes:**
- Missing .NET runtime
- Database connection issues
- Configuration errors
- Permission problems

**Solutions:**
1. Check stdout logs in `logs` folder
2. Verify .NET runtime installation
3. Test database connectivity
4. Check application permissions
5. Review Event Viewer for detailed errors

### 2. Database Connection Issues

**Check:**
- Connection string format
- Database server accessibility from IIS server
- Firewall rules
- Authentication credentials

### 3. Auth0 Configuration Issues

**Verify:**
- Auth0 domain and audience in appsettings
- Network connectivity to Auth0 (https://your-domain.auth0.com/.well-known/jwks.json)
- HTTPS requirements if enforced

## Testing Your Deployment

### 1. Health Check
```bash
curl https://yoursite.com/api/auth/health
```

Expected response:
```json
{
  "status": "Healthy",
  "timestamp": "2025-07-24T...",
  "message": "BurningFury API is running",
  "database": "Connected",
  "environment": "Production"
}
```

### 2. Public Endpoints
```bash
# Test public player listing
curl https://yoursite.com/api/players

# Test Auth0 configuration
curl https://yoursite.com/api/auth/config
```

### 3. Protected Endpoints
```bash
# Test with valid Auth0 token
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" \
     https://yoursite.com/api/auth/validate
```

## Security Considerations

1. **HTTPS**: Always use HTTPS in production
2. **Connection Strings**: Use environment variables or secure configuration
3. **Logging**: Ensure logs don't contain sensitive information
4. **CORS**: Configure CORS appropriately for your domains
5. **Database**: Use least-privilege database accounts

## Monitoring

1. **Health Endpoint**: Monitor `/api/auth/health` for service status
2. **Logs**: Monitor application and IIS logs
3. **Event Viewer**: Watch for ASP.NET Core and IIS events
4. **Database**: Monitor database connectivity and performance

## File Structure After Deployment

```
C:\inetpub\wwwroot\BurningFuryApi\
??? BurningFuryApi.dll
??? BurningFuryApi.exe
??? BurningFuryApi.deps.json
??? BurningFuryApi.runtimeconfig.json
??? web.config
??? appsettings.json
??? appsettings.Production.json
??? wwwroot/
??? logs/ (create this folder)
?   ??? stdout_*.log
??? [other DLLs and dependencies]
```