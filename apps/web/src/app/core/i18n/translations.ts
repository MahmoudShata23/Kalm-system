import type { Language } from "./language.service";

export interface ShellCopy {
  brand: string;
  location: string;
  heading: string;
  summary: string;
  sessionState: string;
  apiLabel: string;
  apiValue: string;
  accessLabel: string;
  accessValue: string;
  languageToggleLabel: string;
  languageLabel: string;
  languageValue: string;
}

export const TRANSLATIONS: Record<Language, ShellCopy> = {
  en: {
    brand: "Kalm Cafe",
    location: "Cairo branch",
    heading: "Kalm Cafe",
    summary: "Calm service, clear rhythm, and warm hospitality for every shift.",
    sessionState: "Not signed in",
    apiLabel: "API",
    apiValue: "Health ready",
    accessLabel: "Access",
    accessValue: "Staff only",
    languageToggleLabel: "Language",
    languageLabel: "Language",
    languageValue: "English"
  },
  ar: {
    brand: "كالم كافيه",
    location: "فرع القاهرة",
    heading: "كالم كافيه",
    summary: "خدمة هادئة وإيقاع واضح وضيافة دافئة في كل وردية.",
    sessionState: "لم يتم تسجيل الدخول",
    apiLabel: "الخدمة",
    apiValue: "جاهزة",
    accessLabel: "الدخول",
    accessValue: "للموظفين فقط",
    languageToggleLabel: "اللغة",
    languageLabel: "اللغة",
    languageValue: "العربية"
  }
};
