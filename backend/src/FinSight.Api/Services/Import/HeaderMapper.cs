using FinSight.Dtos;

namespace FinSight.Services.Import;

public enum MappableField
{
    Date,
    Description,
    Amount,
    Debit,
    Credit,
    Type,
    Category,
    Account,
    Reference
}

/// <summary>Heuristic mapping of CSV headers to application fields.</summary>
public static class HeaderMapper
{
    private static readonly Dictionary<MappableField, string[]> Keywords = new()
    {
        [MappableField.Date] = ["data", "data transacao", "data da transacao", "data de transacao", "data lancamento", "data do lancamento", "data movimento", "data valor", "date", "transaction date", "posted date", "value date", "dia"],
        [MappableField.Description] = ["descricao", "description", "designacao", "detalhe", "detalhes", "details", "merchant", "estabelecimento", "nome", "concepto", "conceito", "movimento", "transaction", "libelle", "label"],
        [MappableField.Amount] = ["valor", "montante", "amount", "value", "importe", "importancia", "quantia", "valor total", "total", "saldo"],
        [MappableField.Debit] = ["debito", "debitos", "debit", "debits", "saida", "saidas", "withdrawal", "withdrawals", "pagamentos", "gastos"],
        [MappableField.Credit] = ["credito", "creditos", "credit", "credits", "entrada", "entradas", "deposito", "depositos", "recebimentos", "deposit"],
        [MappableField.Type] = ["tipo", "type", "natureza", "tipo de transacao", "tipo transacao", "tipo movimento", "movimento tipo"],
        [MappableField.Category] = ["categoria", "category", "rubrica", "grupo", "tipo de despesa", "classe"],
        [MappableField.Account] = ["conta", "account", "origem", "source", "banco", "metodo de pagamento", "metodo pagamento", "cartao", "destino", "destination"],
        [MappableField.Reference] = ["referencia", "reference", "ref", "id transacao", "transaction id", "identificador", "nr movimento", "documento", "doc"]
    };

    public static ColumnMappingDto Detect(IReadOnlyList<string> headers)
    {
        var used = new HashSet<int>();
        var mapping = new ColumnMappingDto();

        foreach (var field in new[]
                 {
                     MappableField.Date, MappableField.Description, MappableField.Amount,
                     MappableField.Debit, MappableField.Credit, MappableField.Type,
                     MappableField.Category, MappableField.Account, MappableField.Reference
                 })
        {
            var bestIndex = -1;
            var bestScore = 0;
            for (var i = 0; i < headers.Count; i++)
            {
                if (used.Contains(i)) continue;
                var score = ScoreHeader(headers[i], Keywords[field]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }
            if (bestIndex < 0 || bestScore <= 0) continue;

            used.Add(bestIndex);
            mapping = field switch
            {
                MappableField.Date => mapping with { DateColumn = bestIndex },
                MappableField.Description => mapping with { DescriptionColumn = bestIndex },
                MappableField.Amount => mapping with { AmountColumn = bestIndex },
                MappableField.Debit => mapping with { DebitColumn = bestIndex },
                MappableField.Credit => mapping with { CreditColumn = bestIndex },
                MappableField.Type => mapping with { TypeColumn = bestIndex },
                MappableField.Category => mapping with { CategoryColumn = bestIndex },
                MappableField.Account => mapping with { AccountColumn = bestIndex },
                MappableField.Reference => mapping with { ReferenceColumn = bestIndex },
                _ => mapping
            };
        }

        return mapping;
    }

    private static int ScoreHeader(string? header, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(header)) return 0;
        var normalized = TextNormalizer.NormalizeKey(header);
        var best = 0;
        foreach (var keyword in keywords)
        {
            if (normalized == keyword) best = Math.Max(best, 3);
            else if (normalized.Contains(keyword)) best = Math.Max(best, 2);
        }
        return best;
    }
}
