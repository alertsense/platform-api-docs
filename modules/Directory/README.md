# Directory Module

The Konexus Directory Module is central to the platform and provides clients and partners the ability to manage Users, Groups, Roles, and related resources.

[OpenApi Specification](directory-openapi-spec.json)

# Change Log

## Version 4.3.0

### Breaking Changes
- Standardized the schema for responses that result in a 400 status code. Changed from BadRequestResponse to ValidationProblemDetails

### Addtions and Enhancements
- Added support to retrieve meta information for Weather Subscriptions and the packaging configured for a specific TenantId.
- Extended the UserProfile to include additional details about the user. These additions are more typically used for internal user use casees. The additional pieces of information relate to:
    - Organization
        - Includes JobTitle and Department
    - Authorization
        - Includes the Role the UserProfile may be assigned to.
    - LegacyAuthorization
        - Includes a collection of groups they're authorized to access

## Version 4.2.3

### Additions and Enhancements
- Added support for Weather Categories and Weather preferences
- Added support for Querying for Users by Phone Number

### Bug Fixes
- Resolved an issue that occurred when creating a new User Location where the API was returning an incorrect response code (200 OK) according to the API Specification (201 Created).
- Resolved issue with Location IDs not being deterministic

## Version 4.2.2

### Bug Fixes
- Resolved Phone Verification Code issues

## Version 4.2.1

### Additions and Enhancements
- Added support to Create an ExternalId on an existing UserProfile
- Added support to Delete an existing UserProfile - Note this is a Soft Delete in the system and recreating the same user may reuse the previous resource

## Version 4.2.0

### Additions and Enhancements
- Added support for creating users with ExternalId and looking up users by their ExternalId
- Updated most 400 level error responses with clearer error messages.

### Bug Fixes
- Resolved an issue with Written and Spoken language preferences not being persisted

### Breaking Changes:
- Updated CreatePhoneCommand to return the PhoneNumber created in the response
- Removed Default property on GroupSummary into a separate objected called SelfServiceSettings that has a Default property and will be used for future expansion.
 

## Version 4.1.0

### Additions and Enhancements
- The Platform API is now fully connected to the Tenant associated with the Identity that is authenticated through Client Credentials.
### Bug Fixes
- No bug fixes
### Breaking Changes:

- Changed the Meta Language and Timezone APIs to return the settings for the TenantId specified instead of by Realm.
- Changed the Open API Spec Tag from User to Users which will cause generated client changes
- Standardized OperationIds, Command names, and request structure:
    - Renamed AddEmailCommand to CreateEmailCommand
        - Renamed 
    - Renamed AddPhoneNumberCommand to CreatePhoneCommand
    - Renamed AddLocationCommand to CreateLocationCommand
    - Renamed Users_GetEmailResendVerificationToken to Users_CreateEmailVerificationNonce
    - Changed the payload of Users_ResendEmailVerification to be an object instead of a string
    - Changed the payload of Users_VerifyEmail to be an object instead of a string
    - Renamed User_GetPhoneResendVerificationToken to Users_CreatePhoneVerificationNonce
    - Changed the payload of Users_ResendPhoneVerification to be an object instead of a string
    - Changed the payload of Users_VerifyPhone to be an object instead of a string
    - Changed the Email, Phones, and Membership collections from arrays to objects to support future paging
 



# Samples and Clients
- [C# Client and Tests](../../samples/Directory/csharp)

# Example Use Cases

## Platform Metadata Discovery

Platform Metadata discovery can be useful to retrieve available and allowed values for other interactions within the system. 

Examples of how this is useful is:
- Determining Available Language Preferences
- Determining Compatible Timezones

Retrieving Available Languages from the Metadata API can assist in displaying supported languages for different types of automatic translation. This prevents the integrator from maintaining their own mapping table locally. Retrieving available languages is preferred as the configuration can be dynamic depending on the availability of languages across the different translation targets. (Text Translation and Text-To-Speech Translation) Additional languages have been added based on client demand.

## New User Registration
- User Profile Creation
- Group Membership Association
- Contact Device Verification

## Existing User Management
- User Profile Modification
- Group Membership Association
- Contact Device Verification
