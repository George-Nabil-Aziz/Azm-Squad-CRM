import { describe, expect, it } from 'vitest'
import { analyzeSms } from './sms-segments'

describe('analyzeSms (same vectors as the server SmsSegmentsTests)', () => {
  it.each([
    [0, 0],
    [1, 1],
    [160, 1],
    [161, 2],
    [306, 2],
    [307, 3],
  ])('GSM-7 text of %i characters needs %i segments', (length, segments) => {
    expect(analyzeSms('a'.repeat(length)).segments).toBe(segments)
  })

  it('counts extension characters twice', () => {
    expect(analyzeSms('€'.repeat(80))).toMatchObject({ units: 160, segments: 1 })
    expect(analyzeSms('€'.repeat(81)).segments).toBe(2)
  })

  it.each([
    [70, 1],
    [71, 2],
    [134, 2],
    [135, 3],
  ])('Arabic text of %i characters needs %i segments (UCS-2)', (length, segments) => {
    expect(analyzeSms('ع'.repeat(length))).toMatchObject({ encoding: 'ucs2', segments })
  })

  it('one non-GSM character makes the whole text UCS-2', () => {
    expect(analyzeSms('a'.repeat(70) + 'ع')).toMatchObject({ encoding: 'ucs2', segments: 2 })
  })
})
