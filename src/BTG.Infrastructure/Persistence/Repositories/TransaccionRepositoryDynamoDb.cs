using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BTG.Application.Interfaces;
using BTG.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace BTG.Infrastructure.Repositories;

public class TransaccionRepositoryDynamoDb : ITransaccionRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _table;

    public TransaccionRepositoryDynamoDb(IAmazonDynamoDB ddb, IConfiguration cfg)
    {
        _ddb = ddb;
        _table = cfg["TransaccionesTable"] ?? "Transacciones";
    }

    public async Task AddAsync(Transaccion t, CancellationToken ct)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["Id"] = new AttributeValue { S = t.Id.ToString() },
            ["ClienteId"] = new AttributeValue { S = t.ClienteId.ToString() },
            ["FondoId"] = new AttributeValue { S = t.FondoId },
            ["Tipo"] = new AttributeValue { S = t.Tipo },
            ["Monto"] = new AttributeValue { N = t.Monto.ToString() },
            ["Fecha"] = new AttributeValue { S = t.Fecha.ToString("o") }, // ISO8601
            ["Estado"] = new AttributeValue { S = t.Estado }
        };

        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _table,
            Item = item
        }, ct);
    }

    public async Task<List<Transaccion>> GetByClienteAsync(Guid clienteId, CancellationToken ct)
    {
        //  Ideal: tener un GSI con ClienteId como partition key
        var scan = new ScanRequest
        {
            TableName = _table,
            FilterExpression = "ClienteId = :c",
            ExpressionAttributeValues = new()
            {
                [":c"] = new AttributeValue { S = clienteId.ToString() }
            }
        };

        var resp = await _ddb.ScanAsync(scan, ct);
        return resp.Items.Select(MapToTx).ToList();
    }

    private Transaccion MapToTx(Dictionary<string, AttributeValue> item)
    {
        return new Transaccion
        {
            Id = Guid.Parse(item["Id"].S),
            ClienteId = Guid.Parse(item["ClienteId"].S),
            FondoId = item["FondoId"].S,
            Tipo = item["Tipo"].S,
            Monto = decimal.Parse(item["Monto"].N),
            Fecha = DateTime.Parse(item["Fecha"].S, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind),
            Estado = item["Estado"].S
        };
    }
}
