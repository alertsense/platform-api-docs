# Instructions for Generating Samples

In order to install the required tools, you will need to have `npm` installed on your system and available from your terminal.

## 1. Install the Open API Generator CLI 
You can install the generated with npm or follow the [installation instructions.](https://openapi-generator.tech/docs/installation/)

```
npm install @openapitools/openapi-generator-cli -g
```

## 2. Generate Directory Sample Client

In order to generate the csharp client for the Directory module, run the following command.

```
npm run openapi:directory-copy
npm run openapi:directory-csharp

# or run both commands
npm run openapi:directory-copy ; npm run openapi:directory-csharp
```

[View Directory csharp Sample](samples/Directory/csharp)