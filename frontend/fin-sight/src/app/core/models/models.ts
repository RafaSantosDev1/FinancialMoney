export type TransactionType = 'Income' | 'Expense';

export enum TransactionTypeValue {
  Income = 0,
  Expense = 1,
}

export type CategoryAppliesTo = 'Expense' | 'Income' | 'Both';

export enum CategoryAppliesToValue {
  Expense = 0,
  Income = 1,
  Both = 2,
}

export type AccountType = 'Checking' | 'Savings' | 'CreditCard' | 'Cash' | 'Investment' | 'Other';

export enum AccountTypeValue {
  Checking = 0,
  Savings = 1,
  CreditCard = 2,
  Cash = 3,
  Investment = 4,
  Other = 5,
}

export type BudgetHealth = 'NoLimit' | 'Ok' | 'Warning' | 'Exceeded';

export enum BudgetHealthValue {
  NoLimit = 0,
  Ok = 1,
  Warning = 2,
  Exceeded = 3,
}

export type InsightSeverity = 'Info' | 'Positive' | 'Warning' | 'Danger';

export enum InsightSeverityValue {
  Info = 0,
  Positive = 1,
  Warning = 2,
  Danger = 3,
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Category {
  id: number;
  name: string;
  color: string;
  appliesTo: CategoryAppliesTo;
  isArchived: boolean;
  isDemo: boolean;
}

export interface Account {
  id: number;
  name: string;
  accountType: AccountType;
  isArchived: boolean;
  isDemo: boolean;
}

export interface Transaction {
  id: number;
  date: string;
  description: string;
  amount: number;
  type: TransactionType;
  categoryId: number;
  categoryName: string;
  categoryColor: string;
  accountId: number | null;
  accountName: string | null;
  notes: string | null;
  externalReference: string | null;
  importBatchId: number | null;
  createdAt: string;
}

export interface TransactionCreateRequest {
  date: string;
  description: string;
  amount: number;
  type: TransactionType;
  categoryId: number;
  accountId: number | null;
  notes: string | null;
}

export type TransactionUpdateRequest = TransactionCreateRequest;

export interface TransactionQuery {
  from?: string;
  to?: string;
  categoryIds?: number[];
  type?: TransactionType | null;
  accountId?: number | null;
  search?: string;
  page: number;
  pageSize: number;
  sortBy: string;
  descending: boolean;
}

export interface Budget {
  id: number;
  categoryId: number | null;
  categoryName: string | null;
  year: number;
  month: number;
  limitAmount: number;
  isDemo: boolean;
}

export interface BudgetStatus {
  categoryId: number | null;
  categoryName: string | null;
  categoryColor: string | null;
  year: number;
  month: number;
  limitAmount: number;
  spentAmount: number;
  remainingAmount: number;
  spentPercent: number;
  health: BudgetHealth;
  isDemo: boolean;
}

export interface MetricDelta {
  current: number;
  previous: number;
  absoluteChange: number;
  percentChange: number | null;
  higherIsBetter: boolean;
}

export interface BudgetUsage {
  hasBudgets: boolean;
  totalLimit: number;
  totalSpent: number;
  remainingAmount: number;
  usedPercent: number;
}

export interface PeriodComparison {
  from: string;
  to: string;
  totalIncome: number;
  totalExpenses: number;
  netBalance: number;
  incomeDelta: MetricDelta;
  expenseDelta: MetricDelta;
  balanceDelta: {
    current: number;
    previous: number;
    absoluteChange: number;
    percentChange: number | null;
  };
  hasComparison: boolean;
}

export interface DashboardSummary {
  from: string;
  to: string;
  daysInPeriod: number;
  monthsInPeriod: number;
  totalIncome: number;
  totalExpenses: number;
  netBalance: number;
  transactionCount: number;
  incomeCount: number;
  expenseCount: number;
  averageExpensePerTransaction: number | null;
  averageDailyExpense: number | null;
  averageMonthlyExpense: number | null;
  savingsRate: number | null;
  previousPeriod: PeriodComparison;
  budgetUsage: BudgetUsage;
}

export interface MonthlyTrendPoint {
  year: number;
  month: number;
  label: string;
  income: number;
  expenses: number;
  balance: number;
  transactionCount: number;
}

export interface CategoryBreakdownItem {
  categoryId: number;
  categoryName: string;
  color: string;
  amount: number;
  percentOfTotal: number;
  transactionCount: number;
}

export interface WeekdaySpendingItem {
  weekday: number;
  label: string;
  totalAmount: number;
  averagePerOccurrence: number;
  transactionCount: number;
}

export interface TopExpenseItem {
  id: number;
  date: string;
  description: string;
  amount: number;
  categoryName: string;
  color: string;
  accountName: string | null;
}

export interface Insight {
  kind: string;
  title: string;
  description: string;
  severity: InsightSeverity;
  isPattern: boolean;
  value: number | null;
}

export enum ImportRowStatusValue {
  Valid = 0,
  Invalid = 1,
  Duplicate = 2,
}

export interface ColumnMapping {
  dateColumn: number;
  descriptionColumn: number;
  amountColumn: number;
  typeColumn: number | null;
  categoryColumn: number | null;
  accountColumn: number | null;
  referenceColumn: number | null;
  debitColumn: number | null;
  creditColumn: number | null;
}

export interface ImportOptions {
  delimiter: string | null;
  dateFormat: string | null;
  decimalSeparator: string | null;
  hasHeader: boolean;
  defaultType: TransactionType | null;
}

export interface ImportPreviewRequest {
  fileName: string;
  content: string;
  options: ImportOptions;
  mapping: ColumnMapping | null;
}

export interface ImportPreviewRow {
  lineNumber: number;
  date: string | null;
  dateRaw: string | null;
  description: string | null;
  amount: number | null;
  debitAmount: number | null;
  creditAmount: number | null;
  type: TransactionType | null;
  categoryName: string | null;
  accountName: string | null;
  reference: string | null;
  status: number;
  errors: string[];
}

export interface ImportRowError {
  lineNumber: number;
  errors: string[];
}

export interface ImportDuplicateGroup {
  key: string;
  fileLineNumbers: number[];
  existingMatches: number;
}

export interface ImportValidationSummary {
  totalRows: number;
  validRows: number;
  invalidRows: number;
  duplicateRows: number;
}

export interface ImportPreviewResponse {
  fileName: string;
  delimiter: string;
  hasHeader: boolean;
  headers: string[];
  detectedMapping: ColumnMapping;
  effectiveMapping: ColumnMapping;
  detectedDateFormat: string;
  detectedDecimalSeparator: string;
  summary: ImportValidationSummary;
  rows: ImportPreviewRow[];
  errors: ImportRowError[];
  duplicateGroups: ImportDuplicateGroup[];
  warnings: string[];
  ambiguousTypeInference: boolean;
  typeInferenceSource: string;
}

export interface ImportConfirmRequest {
  fileName: string;
  content: string;
  options: ImportOptions;
  mapping: ColumnMapping;
  importDuplicates: boolean;
}

export interface ImportResult {
  batchId: number;
  fileName: string;
  importedAt: string;
  totalRows: number;
  importedRows: number;
  rejectedRows: number;
  duplicateRows: number;
  rejectedRowsDetail: ImportRowError[];
  warnings: string[];
  createdCategories: string[];
  createdAccounts: string[];
}

export interface ImportBatch {
  id: number;
  fileName: string;
  importedAt: string;
  totalRows: number;
  importedRows: number;
  rejectedRows: number;
  duplicateRows: number;
  sampleTransactions: Transaction[];
}

export interface SeedStatus {
  hasDemoData: boolean;
  demoCategories: number;
  demoBudgets: number;
  demoTransactions: number;
}

export interface SeedResult {
  categories: number;
  budgets: number;
  transactions: number;
  from: string;
  to: string;
}
