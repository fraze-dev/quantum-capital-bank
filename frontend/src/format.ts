import type { TransactionSummary } from './api/types'

const currencyFormatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const signedCurrencyFormatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  signDisplay: 'exceptZero',
})
const dateTimeFormatter = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' })

export function formatCurrency(amount: number): string {
  return currencyFormatter.format(amount)
}

export function formatSignedCurrency(amount: number): string {
  return signedCurrencyFormatter.format(amount)
}

export function formatDateTime(iso: string): string {
  return dateTimeFormatter.format(new Date(iso))
}

export function describeTransaction(transaction: TransactionSummary): string {
  switch (transaction.type) {
    case 'Deposit':
      return 'Deposit'
    case 'Withdrawal':
      return 'Withdrawal'
    case 'TransferIn':
      return `Transfer from ${transaction.counterpartyAccountNumber}`
    case 'TransferOut':
      return `Transfer to ${transaction.counterpartyAccountNumber}`
    case 'OpeningBalance':
      return 'Opening balance'
  }
}
