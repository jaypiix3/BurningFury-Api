# BurningFury API with Auth0 Integration

This API provides player management functionality for BurningFury with Auth0 authentication.

## Auth0 Setup Instructions

### 1. Create an Auth0 Account
1. Go to [Auth0](https://auth0.com) and create a free account
2. Create a new tenant for your application

### 2. Create an API in Auth0
1. Go to **Applications** > **APIs** in your Auth0 Dashboard
2. Click **Create API**
3. Fill in the details:
   - **Name**: BurningFury API
   - **Identifier**: `https://burningfury.api` (this will be your audience)
   - **Signing Algorithm**: RS256
4. Click **Create**

### 3. Create an Application (Optional - for testing)
1. Go to **Applications** > **Applications**
2. Click **Create Application**
3. Choose **Machine to Machine Applications**
4. Select your BurningFury API
5. Authorize the application and select scopes if needed

### 4. Configure the API Settings

Update your `appsettings.json` and `appsettings.Development.json` files:

```json
{
  "Auth0": {
    "Domain": "your-auth0-domain.auth0.com",
    "Audience": "https://burningfury.api"
  }
}
```

Replace:
- `your-auth0-domain.auth0.com` with your actual Auth0 domain
- `https://burningfury.api` with your API identifier

## Testing the API

### 1. Get a Test Token

To test the API, you'll need to obtain a JWT token from Auth0. You can do this in several ways:

#### Option A: Using Auth0's Test Tab
1. Go to your API in the Auth0 Dashboard
2. Click on the **Test** tab
3. Copy the test token provided

#### Option B: Using curl to get a token
```bash
curl --request POST \
  --url 'https://YOUR_DOMAIN.auth0.com/oauth/token' \
  --header 'content-type: application/json' \
  --data '{
    "client_id": "YOUR_CLIENT_ID",
    "client_secret": "YOUR_CLIENT_SECRET",
    "audience": "https://burningfury.api",
    "grant_type": "client_credentials"
  }'
```

### 2. Test Endpoints

#### Public Endpoints (No Authentication Required)
- `GET /api/auth/health` - Health check
- `GET /api/auth/config` - Auth0 configuration

#### Protected Endpoints (Authentication Required)
- `GET /api/auth/validate` - Validate your token
- `GET /api/players/me` - Get current user info
- `GET /api/players` - Get all players
- `GET /api/players/{id}` - Get specific player
- `POST /api/players` - Create a new player
- `DELETE /api/players/{id}` - Delete a player

### 3. Using Swagger UI

1. Start the application
2. Navigate to the root URL (Swagger UI)
3. Click the **Authorize** button
4. Enter your token in the format: `Bearer YOUR_JWT_TOKEN`
5. Test the protected endpoints

### 4. Using curl with Authorization

```bash
# Test with valid token
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" \
     -H "Content-Type: application/json" \
     https://localhost:7000/api/players

# Test token validation
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" \
     https://localhost:7000/api/auth/validate
```

## API Features

### Authentication Features
- ? JWT token validation against Auth0
- ? Automatic token expiration handling
- ? User context extraction from JWT claims
- ? Comprehensive error handling for auth failures
- ? Swagger UI integration with JWT authentication
- ? CORS support for frontend applications

### Security Features
- ? All player endpoints protected by default
- ? User activity logging with Auth0 user IDs
- ? Token validation with proper error responses
- ? Configurable Auth0 settings via appsettings

### Available Claims
The API extracts the following information from JWT tokens:
- User ID (`sub` claim)
- Email (`email` claim)
- Name (`name` claim)
- All other custom claims from Auth0

## Development Notes

- The API uses Auth0's RS256 algorithm for token validation
- Clock skew is set to zero for strict token validation
- All authentication errors are logged for debugging
- The middleware provides user-friendly error messages for auth failures

## Production Considerations

1. **Environment Variables**: Store Auth0 settings in environment variables in production
2. **HTTPS**: Ensure the API runs over HTTPS in production
3. **CORS**: Configure CORS appropriately for your frontend domains
4. **Logging**: Monitor authentication logs for security events
5. **Rate Limiting**: Consider implementing rate limiting for API endpoints

## Troubleshooting

### Common Issues

1. **401 Unauthorized**: 
   - Check if your token is valid and not expired
   - Verify the Auth0 domain and audience configuration
   - Ensure the token is sent in the correct format: `Bearer TOKEN`

2. **Token Validation Errors**:
   - Verify your Auth0 domain is correct
   - Check if the API identifier matches your audience
   - Ensure your Auth0 API is using RS256 signing

3. **CORS Issues**:
   - Check if CORS is properly configured for your frontend domain
   - Verify preflight requests are handled correctly

Use the `/api/auth/validate` endpoint to debug token issues and view all claims.