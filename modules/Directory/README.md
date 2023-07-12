# Directory Module

The Konexus Directory Module is central to the platform and provides clients and partners the ability to manage Users, Groups, Roles, and related resources.

[OpenApi Specification](directory-openapi-spec.json)

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
