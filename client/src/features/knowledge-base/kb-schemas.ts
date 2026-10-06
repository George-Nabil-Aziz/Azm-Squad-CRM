import type { TFunction } from 'i18next'
import { z } from 'zod'

/**
 * Client-side checks of the article dialog (the server validates again). A language version is a title and a body,
 * both or neither; at least one title is needed.
 */
export function createArticleFormSchema(t: TFunction) {
  return z
    .object({
      categoryId: z.string().min(1, t('knowledgeBase.categoryRequired')),
      titleEn: z.string().trim().max(200),
      bodyEn: z.string().trim().max(20_000),
      titleAr: z.string().trim().max(200),
      bodyAr: z.string().trim().max(20_000),
    })
    .superRefine((values, context) => {
      if (!values.titleEn && !values.titleAr) {
        context.addIssue({ code: 'custom', path: ['titleEn'], message: t('knowledgeBase.titleRequired') })
      }
      if (values.titleEn && !values.bodyEn) {
        context.addIssue({ code: 'custom', path: ['bodyEn'], message: t('knowledgeBase.bodyRequired') })
      }
      if (values.bodyEn && !values.titleEn) {
        context.addIssue({ code: 'custom', path: ['titleEn'], message: t('knowledgeBase.titleRequiredForBody') })
      }
      if (values.titleAr && !values.bodyAr) {
        context.addIssue({ code: 'custom', path: ['bodyAr'], message: t('knowledgeBase.bodyRequired') })
      }
      if (values.bodyAr && !values.titleAr) {
        context.addIssue({ code: 'custom', path: ['titleAr'], message: t('knowledgeBase.titleRequiredForBody') })
      }
    })
}

export type ArticleFormValues = z.infer<ReturnType<typeof createArticleFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys); `title` is shown at the English title. */
export const articleFormFields = ['categoryId', 'titleEn', 'bodyEn', 'titleAr', 'bodyAr'] as const

export function createKbCategoryFormSchema(t: TFunction) {
  return z
    .object({ nameEn: z.string().trim().max(100), nameAr: z.string().trim().max(100) })
    .refine((values) => values.nameEn || values.nameAr, { path: ['nameEn'], message: t('knowledgeBase.nameRequired') })
}

export type KbCategoryFormValues = z.infer<ReturnType<typeof createKbCategoryFormSchema>>

export const kbCategoryFormFields = ['nameEn', 'nameAr'] as const
