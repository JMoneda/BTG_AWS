using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BTG.Application.Interfaces;
using BTG.Domain.Entities;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace BTG.Infrastructure.Repositories;

public class ClienteRepositoryDynamoDb : IClienteRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _table;

    public ClienteRepositoryDynamoDb(IAmazonDynamoDB ddb, IConfiguration cfg)
    {
        _ddb = ddb;
        _table = cfg["ClientesTable"] ?? "Clientes"; // nombre desde env vars (serverless.template)
    }

    public async Task AddAsync(Cliente cliente, CancellationToken ct)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["Id"] = new AttributeValue { S = cliente.Id.ToString() },
            ["Nombre"] = new AttributeValue { S = cliente.Nombre },
            ["Email"] = new AttributeValue { S = cliente.Email },
            ["Telefono"] = new AttributeValue { S = cliente.Telefono ?? string.Empty },
            ["Saldo"] = new AttributeValue { N = cliente.Saldo.ToString() },
            ["PreferenciaNotificacion"] = new AttributeValue { N = ((int)cliente.PreferenciaNotificacion).ToString() }
        };

        // FondosActivos -> por ahora lista vacía
        if (cliente.FondosActivos != null && cliente.FondosActivos.Any())
        {
            item["FondosActivos"] = new AttributeValue
            {
                SS = cliente.FondosActivos.Select(f => f.FondoId).ToList()
            };
        }

        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _table,
            Item = item
        }, ct);
    }

    public async Task<Cliente?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = id.ToString() }
            }
        }, ct);

        if (resp.Item == null || resp.Item.Count == 0) return null;

        return MapToCliente(resp.Item);
    }

    public async Task<Cliente?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var req = new QueryRequest
        {
            TableName = _table,
            IndexName = "Email-index", // requiere GSI
            KeyConditionExpression = "Email = :email",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":email"] = new AttributeValue { S = email }
            }
        };

        var resp = await _ddb.QueryAsync(req, ct);
        var item = resp.Items.FirstOrDefault();

        return item is null ? null : MapToCliente(item);
    }

    public async Task UpdateAsync(Cliente cliente, CancellationToken ct)
    {
        var itemFondos = cliente.FondosActivos?.Select(f => new Dictionary<string, AttributeValue>
        {
            ["FondoId"] = new AttributeValue { S = f.FondoId },
            ["Nombre"] = new AttributeValue { S = f.Nombre },
            ["Monto"] = new AttributeValue { N = f.Monto.ToString() },
            ["FechaVinculacion"] = new AttributeValue { S = f.FechaVinculacion.ToString("o") }
        }).ToList();

        var req = new UpdateItemRequest
        {
            TableName = _table,
            Key = new() { ["Id"] = new AttributeValue { S = cliente.Id.ToString() } },
            UpdateExpression = "SET Nombre = :n, Email = :e, Telefono = :t, Saldo = :s, PreferenciaNotificacion = :p, FondosActivos = :f",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":n"] = new AttributeValue { S = cliente.Nombre },
                [":e"] = new AttributeValue { S = cliente.Email },
                [":t"] = new AttributeValue { S = cliente.Telefono },
                [":s"] = new AttributeValue { N = cliente.Saldo.ToString() },
                [":p"] = new AttributeValue { N = ((int)cliente.PreferenciaNotificacion).ToString() },
                [":f"] = new AttributeValue { L = itemFondos?.Select(f => new AttributeValue { M = f }).ToList() ?? new List<AttributeValue>() }
            }
        };

        await _ddb.UpdateItemAsync(req, ct);
    }

    private Cliente MapToCliente(Dictionary<string, AttributeValue> item)
    {
        var cliente = new Cliente
        {
            Id = Guid.Parse(item["Id"].S),
            Nombre = item["Nombre"].S,
            Email = item["Email"].S,
            Telefono = item.ContainsKey("Telefono") ? item["Telefono"].S : string.Empty,
            Saldo = item.ContainsKey("Saldo") ? decimal.Parse(item["Saldo"].N, CultureInfo.InvariantCulture) : 0,
            PreferenciaNotificacion = item.ContainsKey("PreferenciaNotificacion")
                                      ? (PreferenciaNotificacion)int.Parse(item["PreferenciaNotificacion"].N)
                                      : (PreferenciaNotificacion)0,
            FondosActivos = new List<FondoActivo>()
        };

        if (item.ContainsKey("FondosActivos") && item["FondosActivos"].L != null)
        {
            cliente.FondosActivos.AddRange(
                item["FondosActivos"].L.Select(fa =>
                {
                    var dict = fa.M;
                    return new FondoActivo
                    {
                        FondoId = dict["FondoId"].S,
                        Nombre = dict.ContainsKey("Nombre") ? dict["Nombre"].S : string.Empty,
                        FechaVinculacion = dict.ContainsKey("FechaVinculacion")
                            ? DateTime.Parse(dict["FechaVinculacion"].S, CultureInfo.InvariantCulture)
                            : DateTime.UtcNow,
                        Monto = dict.ContainsKey("Monto")
                            ? decimal.Parse(dict["Monto"].N, CultureInfo.InvariantCulture)
                            : 0
                    };
                })
            );
        }

        return cliente;
    }


}
