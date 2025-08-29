using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BTG.Application.Interfaces;
using BTG.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace BTG.Infrastructure.Repositories;

public class FondoRepositoryDynamoDb : IFondoRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _table;

    public FondoRepositoryDynamoDb(IAmazonDynamoDB ddb, IConfiguration cfg)
    {
        _ddb = ddb;
        _table = cfg["FondosTable"] ?? "Fondos";
    }

    public async Task<Fondo?> GetByIdAsync(string id, CancellationToken ct)
    {
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            Key = new() { ["Id"] = new AttributeValue { S = id } }
        }, ct);

        if (resp.Item == null || resp.Item.Count == 0) return null;

        return MapToFondo(resp.Item);
    }

    public async Task<List<Fondo>> GetAllAsync(CancellationToken ct)
    {
        var resp = await _ddb.ScanAsync(new ScanRequest { TableName = _table }, ct);
        var fondos = resp.Items.Select(MapToFondo).ToList();
        return fondos.OrderBy(f => int.Parse(f.Id)).ToList();
    }

    public async Task SeedIfEmptyAsync(IEnumerable<Fondo> fondos, CancellationToken ct)
    {
        var resp = await _ddb.ScanAsync(new ScanRequest { TableName = _table, Limit = 1 }, ct);
        if (resp.Count > 0) return;

        foreach (var f in fondos)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = f.Id },
                ["Nombre"] = new AttributeValue { S = f.Nombre },
                ["MontoMinimo"] = new AttributeValue { N = f.MontoMinimo.ToString() },
                ["Categoria"] = new AttributeValue { S = f.Categoria }
            };

            await _ddb.PutItemAsync(new PutItemRequest
            {
                TableName = _table,
                Item = item
            }, ct);
        }
    }

    private Fondo MapToFondo(Dictionary<string, AttributeValue> item)
    {
        return new Fondo
        {
            Id = item["Id"].S,
            Nombre = item["Nombre"].S,
            MontoMinimo = decimal.Parse(item["MontoMinimo"].N),
            Categoria = item["Categoria"].S
        };
    }
}
