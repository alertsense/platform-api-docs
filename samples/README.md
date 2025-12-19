# Instructions for Generating Samples

In order to install the required tools, you will need to have `npm` installed on your system and available from your terminal.

## 1. Install the Open API Generator CLI 
You can install the generated with npm or follow the [installation instructions.](https://openapi-generator.tech/docs/installation/)

```
npm install @openapitools/openapi-generator-cli -g
```

## 2. Generate Sample Client

### Directory Module

In order to generate the csharp client for the Directory module, run the following command.

```
npm run openapi:directory-copy
npm run openapi:directory-csharp

# or run both commands
npm run openapi:directory-copy ; npm run openapi:directory-csharp
```
[View Directory csharp Sample](Directory/csharp)

### File Management Module

In order to generate the csharp client for the File Management module, run the following command.

```
npm run openapi:file-management-csharp
```
[View Files csharp Sample](FileManagement/csharp)

### Alerting Module

In order to generate the csharp client for the Alerting module, run the following command.

```
npm run openapi:alerting-csharp
```
[View Alerting csharp Sample](Alerting/csharp)