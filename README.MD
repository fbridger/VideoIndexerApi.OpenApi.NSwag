# Problem using NSwag to generate VideoIndexer OpenAPI HTTP Client
This repository contains a sample Video Indexer Console application that throws a JsonSerializationException when using Visual Studio Connected Services to generate the OpenAPI Models and HttpClient.

Generating HTTP API clients using Visual Studio Connected Services:
https://devblogs.microsoft.com/dotnet/generating-http-api-clients-using-visual-studio-connected-services/

The Video Indexer Client generated automatically from the OpenAPI specification available in http://api-portal.videoindexer.ai/api-details#api=Operations throws an JsonSerializationException when trying to deserialize the VideoIndexContainer response.

Exception:
```
Inner Exception 1:
JsonSerializationException: Error converting value "en-US" to type 'VideoIndexerApi.LanguageV2'. Path 'videos[0].insights.sourceLanguage', line 1, position 22166.

Inner Exception 2:
ArgumentException: Could not cast or convert from System.String to VideoIndexerApi.LanguageV2.
```

