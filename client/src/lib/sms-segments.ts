/** How a text is sent as SMS (the same rules as the server: Crm.Domain.Channels.SmsSegments). */
export interface SmsSegmentInfo {
  encoding: 'gsm7' | 'ucs2'
  units: number
  segments: number
  singleLimit: number
  multiLimit: number
}

const GSM7_BASIC =
  "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà"
const GSM7_EXTENSION = '^{}\\[~]|€\f'

/**
 * GSM-7 text: 160 characters in one SMS, 153 per part when longer (^ { } \ [ ~ ] | € count twice).
 * Any other character (Arabic, emoji) makes the whole text UCS-2: 70 in one SMS, 67 per part.
 */
export function analyzeSms(text: string): SmsSegmentInfo {
  let gsmUnits = 0
  let isGsm7 = true
  for (const character of text) {
    if (GSM7_BASIC.includes(character)) gsmUnits += 1
    else if (GSM7_EXTENSION.includes(character)) gsmUnits += 2
    else {
      isGsm7 = false
      break
    }
  }
  const [encoding, units, singleLimit, multiLimit] = isGsm7
    ? (['gsm7', gsmUnits, 160, 153] as const)
    : (['ucs2', text.length, 70, 67] as const)
  const segments = units === 0 ? 0 : units <= singleLimit ? 1 : Math.ceil(units / multiLimit)
  return { encoding, units, segments, singleLimit, multiLimit }
}
