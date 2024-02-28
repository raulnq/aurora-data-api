using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.RDSDataService;
using Amazon.RDSDataService.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MyApi;

public class Function
{
    private readonly IAmazonRDSDataService _client;

    private readonly string _secretArn;

    private readonly string _clusterArn;

    private readonly string _database;

    public Function()
    {
        _client = new AmazonRDSDataServiceClient();
        _secretArn = Environment.GetEnvironmentVariable("SECRET_ARN")!;
        _clusterArn = Environment.GetEnvironmentVariable("CLUSTER_ARN")!;
        _database = Environment.GetEnvironmentVariable("DATABASE")!;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> Register(APIGatewayHttpApiV2ProxyRequest input, ILambdaContext context)
    {
        var registerTaskrequest = JsonSerializer.Deserialize<RegisterTaskRequest>(input.Body)!;
        
        var taskId = Guid.NewGuid().ToString();

        var request = new ExecuteStatementRequest()
        {
            Sql = "insert into tasks(Id, Name, Description) VALUES(:id, :name, :description)",
            ResourceArn = _clusterArn,
            SecretArn = _secretArn,
            Parameters = new List<SqlParameter>()
            {
                new SqlParameter(){ Name = "id", Value = new Field(){ StringValue=taskId } },
                new SqlParameter(){ Name = "name", Value = new Field(){ StringValue=registerTaskrequest.Name } },
                new SqlParameter(){ Name = "description", Value = new Field(){ StringValue=registerTaskrequest.Description } }
            },
            Database = _database,
        };

        try
        {
            var response = await _client.ExecuteStatementAsync(request);

            var registerTaskResponse = JsonSerializer.Serialize(new RegisterTaskResponse(taskId));

            return new APIGatewayHttpApiV2ProxyResponse
            {
                Body = registerTaskResponse,
                StatusCode = 200,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
        catch (DatabaseErrorException)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 500,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> Get(APIGatewayHttpApiV2ProxyRequest input, ILambdaContext context)
    {
        var taskId = input.PathParameters["taskId"];

        var request = new ExecuteStatementRequest()
        {
            Sql = "select Id, Name, Description from tasks where id = :id",
            ResourceArn = _clusterArn,
            SecretArn = _secretArn,
            Parameters = new List<SqlParameter>()
            {
                new SqlParameter(){ Name = "id", Value = new Field(){ StringValue=taskId } },
            },
            Database = _database,
            FormatRecordsAs =  RecordsFormatType.JSON
        };

        try
        {
            var response = await _client.ExecuteStatementAsync(request);

            var tasks = JsonSerializer.Deserialize<@Task[]>(response.FormattedRecords, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true })!;

            if(!tasks.Any())
            {
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 404,
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
            }

            var task = JsonSerializer.Serialize(tasks.First());

            return new APIGatewayHttpApiV2ProxyResponse
            {
                Body = task,
                StatusCode = 200,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
        catch (DatabaseErrorException)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 500,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> List(APIGatewayHttpApiV2ProxyRequest input, ILambdaContext context)
    {
        var request = new ExecuteStatementRequest()
        {
            Sql = "select Id, Name, Description from tasks",
            ResourceArn = _clusterArn,
            SecretArn = _secretArn,
            Database = _database,
            FormatRecordsAs = RecordsFormatType.JSON
        };

        try
        {
            var response = await _client.ExecuteStatementAsync(request);

            var tasks = JsonSerializer.Deserialize<@Task[]>(response.FormattedRecords, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true })!;

            return new APIGatewayHttpApiV2ProxyResponse
            {
                Body = JsonSerializer.Serialize(tasks),
                StatusCode = 200,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
        catch (DatabaseErrorException)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 500,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
    }
}

public record RegisterTaskRequest(string Name, string Description);

public record RegisterTaskResponse(string Id);

public record @Task(string Id, string Name, string Description);