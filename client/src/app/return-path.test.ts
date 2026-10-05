import { describe, expect, it } from 'vitest'
import { getReturnPath } from './return-path'

describe('getReturnPath', () => {
  it.each([
    [undefined, '/'],
    [null, '/'],
    [{}, '/'],
    [{ from: 42 }, '/'],
    [{ from: '/customers' }, '/customers'],
    [{ from: '/tickets?status=open' }, '/tickets?status=open'],
    [{ from: '//evil.example' }, '/'],
    [{ from: '/\\evil.example' }, '/'],
    [{ from: 'https://evil.example' }, '/'],
    [{ from: '/login' }, '/'],
  ])('state %j → %s', (state, expected) => {
    expect(getReturnPath(state)).toBe(expected)
  })
})
