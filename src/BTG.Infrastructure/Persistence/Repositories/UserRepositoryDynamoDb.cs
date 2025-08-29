using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BTG.Application.Interfaces;
using BTG.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace BTG.Infrastructure.Repositories;

public class UserRepositoryDynamoDb : IUserRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _table;

    public UserRepositoryDynamoDb(IAmazonDynamoDB ddb, IConfiguration cfg)
    {
        _ddb = ddb;
        _table = cfg["UsuariosTable"] ?? "Usuarios";
    }

    public async Task AddAsync(User user, CancellationToken ct)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["Id"] = new AttributeValue { S = user.Id },
            ["Username"] = new AttributeValue { S = user.Username },
            ["PasswordHash"] = new AttributeValue { S = user.PasswordHash },
            ["Role"] = new AttributeValue { S = user.Role }
            //  Los RefreshTokens podrías serializarlos en JSON si los necesitas
        };

        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _table,
            Item = item,
            ConditionExpression = "attribute_not_exists(Id)" // evita duplicados
        }, ct);
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken ct)
    {
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            Key = new() { ["Id"] = new AttributeValue { S = id } }
        }, ct);

        if (resp.Item == null || resp.Item.Count == 0) return null;
        return MapToUser(resp.Item);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct)
    {
        var query = new QueryRequest
        {
            TableName = _table,
            IndexName = "Username-index", // usar el GSI
            KeyConditionExpression = "Username = :u",
            ExpressionAttributeValues = new()
            {
                [":u"] = new AttributeValue { S = username }
            },
            Limit = 1
        };

        var resp = await _ddb.QueryAsync(query, ct);
        var item = resp.Items.FirstOrDefault();
        return item is null ? null : MapToUser(item);
    }


    public async Task UpdateAsync(User user, CancellationToken ct)
    {
        var req = new UpdateItemRequest
        {
            TableName = _table,
            Key = new() { ["Id"] = new AttributeValue { S = user.Id } },
            UpdateExpression = "SET Username = :u, PasswordHash = :p, #rl = :r",
            ExpressionAttributeValues = new()
            {
                [":u"] = new AttributeValue { S = user.Username },
                [":p"] = new AttributeValue { S = user.PasswordHash },
                [":r"] = new AttributeValue { S = user.Role }
            },
            ExpressionAttributeNames = new()
            {
                ["#rl"] = "Role" 
            }
        };

        await _ddb.UpdateItemAsync(req, ct);
    }


    private User MapToUser(Dictionary<string, AttributeValue> item)
    {
        return new User
        {
            Id = item["Id"].S,
            Username = item["Username"].S,
            PasswordHash = item["PasswordHash"].S,
            Role = item["Role"].S,
            RefreshTokens = new List<RefreshToken>() // inicializamos vacío
        };
    }
}
