namespace FinSight.Domain;

public enum TransactionType
{
    Income = 0,
    Expense = 1
}

public enum CategoryAppliesTo
{
    Expense = 0,
    Income = 1,
    Both = 2
}

public enum AccountType
{
    Checking = 0,
    Savings = 1,
    CreditCard = 2,
    Cash = 3,
    Investment = 4,
    Other = 5
}
