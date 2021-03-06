# datadog-lambda-dotnet

[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](https://github.com/vornet/datadog-lambda-dotnet/blob/main/LICENSE)
![](https://github.com/dotnet/datadog-lambda-dotnet/workflows/Test%20on%20Master%20branch/badge.svg)
![NuGet](https://img.shields.io/nuget/v/Vornet.DataDog.Lambda.Dotnet)

The unofficial Datadog Lambda .NET Client Library for .NET Standard 2.0 enables [enhanced lambda metrics](https://docs.datadoghq.com/integrations/amazon_lambda/?tab=awsconsole#real-time-enhanced-lambda-metrics) and [distributed tracing](https://docs.datadoghq.com/integrations/amazon_lambda/?tab=awsconsole#tracing-with-datadog-apm) between serverful and serverless environments, as well as letting you send [custom metrics](https://docs.datadoghq.com/integrations/amazon_lambda/?tab=awsconsole#custom-metrics) to the Datadog API.  It's a direct port of [datadog-lambda-java](https://github.com/DataDog/datadog-lambda-java)

## Installation

This library will be distributed through NuGet [NuGet](https://img.shields.io/nuget/v/DataDog.Lambda.Dotnet). Follow the [installation instructions](https://docs.datadoghq.com/serverless/installation/dotnet/), and view your function's enhanced metrics, traces and logs in Datadog. 

## Environment Variables

### DD_LOG_LEVEL

Set to `debug` enable debug logs from the Datadog Lambda Library. Defaults to `info`.

### DD_ENHANCED_METRICS

Generate enhanced Datadog Lambda integration metrics, such as, `aws.lambda.enhanced.invocations` and `aws.lambda.enhanced.errors`. Defaults to `true`.

## Enhanced Metrics

Once [installed](#installation), you should be able to view enhanced metrics for your Lambda function in Datadog.

Check out the official documentation on [Datadog Lambda enhanced metrics](https://docs.datadoghq.com/integrations/amazon_lambda/?tab=java#real-time-enhanced-lambda-metrics).

## Custom Metrics

Once [installed](#installation), you should be able to submit custom metrics from your Lambda function.

Check out the instructions for [submitting custom metrics from AWS Lambda functions](https://docs.datadoghq.com/integrations/amazon_lambda/?tab=java#custom-metrics).

## Distributed Tracing

Wrap your outbound HTTP requests with trace headers to see your lambda in context in APM.
The Lambda Java Client Library provides instrumented HTTP connection objects as well as helper methods for
instrumenting HTTP connections made with any of the following libraries:

- java.net.HttpUrlConnection
- Apache HTTP Client
- OKHttp3

Don't see your favorite client? Open an issue and request it. Datadog is adding to 
this library all the time.

### HttpUrlConnection examples

```csharp
public class Handler implements RequestHandler<APIGatewayV2ProxyRequestEvent, APIGatewayV2ProxyResponseEvent> {
    public Integer handleRequest(APIGatewayV2ProxyRequestEvent request, Context context){
        DDLambda dd = new DDLambda(request, lambda);
 
        URL url = new URL("https://example.com");
        HttpURLConnection instrumentedUrlConnection = dd.makeUrlConnection(url); //Trace headers included

        instrumentedUrlConnection.connect();
    
        return 7;
    }
}
```

Alternatively, if you want to do something more complex:

```csharp
public class Handler implements RequestHandler<APIGatewayV2ProxyRequestEvent, APIGatewayV2ProxyResponseEvent> {
    public Integer handleRequest(APIGatewayV2ProxyRequestEvent request, Context context){
        DDLambda dd = new DDLambda(request, lambda);
 
        URL url = new URL("https://example.com");
        HttpURLConnection hc = (HttpURLConnection)url.openConnection();

        //Add the distributed tracing headers
        hc = (HttpURLConnection) dd.addTraceHeaders(hc);

        hc.connect();
    
        return 7;
    }
}
```

### Apache HTTP Client examples

```java
public class Handler implements RequestHandler<APIGatewayV2ProxyRequestEvent, APIGatewayV2ProxyResponseEvent> {
    public Integer handleRequest(APIGatewayV2ProxyRequestEvent request, Context context){
        DDLambda dd = new DDLambda(request, lambda);
    
        HttpClient client = HttpClientBuilder.create().build();
    
        HttpGet hg = dd.makeHttpGet("https://example.com"); //Trace headers included

        HttpResponse hr = client.execute(hg);
        return 7;
    }
}
```

Alternatively, if you want to do something more complex:

```java
public class Handler implements RequestHandler<APIGatewayV2ProxyRequestEvent, APIGatewayV2ProxyResponseEvent> {
    public Integer handleRequest(APIGatewayV2ProxyRequestEvent request, Context context){
        DDLambda dd = new DDLambda(request, lambda);
    
        HttpClient client = HttpClientBuilder.create().build();
        HttpGet hg = new HttpGet("https://example.com");
    
        //Add the distributed tracing headers
        hg = (HttpGet) dd.addTraceHeaders(hg);

        HttpResponse hr = client.execute(hg);
        return 7;
    }
}
```


### OKHttp3 Client examples

```java
public class Handler implements RequestHandler<APIGatewayV2ProxyRequestEvent, APIGatewayV2ProxyResponseEvent> {
    public Integer handleRequest(APIGatewayV2ProxyRequestEvent request, Context context){
        DDLambda dd = new DDLambda(request, lambda);
    
        HttpClient client = HttpClientBuilder.create().build();
        OkHttpClient okHttpClient = new OkHttpClient.Builder().build();
        Request okHttpRequest = dd.makeRequestBuilder() // Trace headers included
            .url("https://example.com")
            .build(); 

        Response resp = okHttpClient.newCall(okHttpRequest).execute();

        return 7;
    }
}
```

Alternatively:

```java
public class Handler implements RequestHandler<APIGatewayV2ProxyRequestEvent, APIGatewayV2ProxyResponseEvent> {
    public Integer handleRequest(APIGatewayV2ProxyRequestEvent request, Context context){
        DDLambda dd = new DDLambda(request, lambda);
    
        HttpClient client = HttpClientBuilder.create().build();
        OkHttpClient okHttpClient = new OkHttpClient.Builder().build();
        Request okHttpRequest = new Request.Builder()
            .url("https://example.com")
            .build();

        //Add the distributed tracing headers
        okHttpRequest = dd.addTraceHeaders(okHttpRequest);

        Response resp = okHttpClient.newCall(okHttpRequest).execute();

        return 7;
    }
}
```

### Trace/Log Correlations

In order to correlate your traces with your logs, you must inject the trace context
into your log messages. We've added the these into the slf4j MDC under the key `dd.trace_context`
and provided convenience methods to get it automatically. The trace context is added to the MDC as a side
effect of instantiating any `new DDLambda(...)`.

This is an example trace context: `[dd.trace_id=3371139772690049666 dd.span_id=13006875827893840236]`

#### JSON Logs

If you are using JSON logs, add the trace ID and span ID to each log message with the keys 
`dd.trace_id` and `dd.span_id` respectively. To get a map containing trace and span IDs,
 call `DDLambda.getTraceContext()`. Union this map with the JSON data being logged.

#### Plain text logs

If you are using plain text logs, then you must create a new [Parser](https://docs.datadoghq.com/logs/processing/parsing/?tab=matcher)
by cloning the existing Lambda Pipeline. The new parser can extract the trace context from the correct position in the logs. 
Use the helper `_trace_context` to extract the trace context. For example, if your log line looked like:

```
INFO 2020-11-11T14:00:00Z LAMBDA_REQUEST_ID [dd.trace_id=12345 dd.span_id=67890] This is a log message
```

Then your parser rule would look like:

```
my_custom_rule \[%{word:level}\]?\s+%{_timestamp}\s+%{notSpace:lambda.request_id}%{_trace_context}?.*
```

#### Log4j / SLF4J

We have added the Trace ID into the slf4j MDC under the key `dd.trace_context`. That can be accessed
using the `%X{dd.trace_context}` operator. Here is an example `log4j.properties`: 

```
log = .
log4j.rootLogger = DEBUG, LAMBDA

log4j.appender.LAMBDA=com.amazonaws.services.lambda.runtime.log4j.LambdaAppender
log4j.appender.LAMBDA.layout=org.apache.log4j.PatternLayout
log4j.appender.LAMBDA.layout.conversionPattern=%d{yyyy-MM-dd HH:mm:ss} %X{dd.trace_context} %-5p %c:%L - %m%n
```

would result in log lines looking like `2020-11-13 19:21:53 [dd.trace_id=1168910694192328743 dd.span_id=3204959397041471598] INFO  com.serverless.Handler:20 - Test Log Message`

Just like the **Plain Text Logs** in the previous section, you must create a new [Parser](https://docs.datadoghq.com/logs/processing/parsing/?tab=matcher) in order to parse the trace context correctly.


#### Other logging solutions

If you are using a different logging solution, the trace ID can be accessed using the method
`DDLambda.getTraceContextString()`. That returns your trace ID as a string that can be added
to any log message.

## Opening Issues

If you encounter a bug with this package, we want to hear about it. Before opening a new issue, 
search the existing issues to avoid duplicates.

When opening an issue, include the Datadog Lambda Layer version, Java version, and stack trace if 
available. In addition, include the steps to reproduce when appropriate.

You can also open an issue for a feature request.

## Contributing

If you find an issue with this package and have a fix, please feel free to open a pull request 
following the [procedures](https://github.com/DataDog/dd-lambda-layer-java/blob/main/CONTRIBUTING.md).

## License

Unless explicitly stated otherwise all files in this repository are licensed under the Apache License Version 2.0.

This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2020 Datadog, Inc.
