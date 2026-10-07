/** SLA compliance (response and resolution together): met / (met + breached), null when nothing is decided yet. */
export function slaCompliancePercent(
  response: { met: number; breached: number },
  resolution: { met: number; breached: number },
): number | null {
  const met = response.met + resolution.met
  const decided = met + response.breached + resolution.breached
  return decided === 0 ? null : Math.round((met / decided) * 1000) / 10
}
