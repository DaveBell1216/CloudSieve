targetScope = 'resourceGroup'

@description('Monthly budget in the subscription billing currency. Alerts do not cap spending.')
@minValue(1)
param monthlyAmount int

@description('Email address to receive budget notifications.')
param contactEmail string

@description('First day of the budget period, e.g. 2026-11-01T00:00:00Z.')
param startDate string

resource budget 'Microsoft.Consumption/budgets@2026-06-01' = {
  name: 'sieve-monthly-budget'
  properties: {
	category: 'Cost'
	amount: monthlyAmount
	timeGrain: 'Monthly'
	timePeriod: { startDate: startDate }
	notifications: {
	  actual80: {
		enabled: true
		operator: 'GreaterThan'
		threshold: 80
		thresholdType: 'Actual'
		contactEmails: [contactEmail]
	  }
	  actual100: {
		enabled: true
		operator: 'GreaterThan'
		threshold: 100
		thresholdType: 'Actual'
		contactEmails: [contactEmail]
	  }
	}
  }
}
