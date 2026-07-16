import { describe, expect, it } from "vitest";
import { LanguageService } from "./language.service";

describe("LanguageService", () => {
  it("defaults to English left-to-right copy", () => {
    const service = new LanguageService();

    expect(service.language()).toBe("en");
    expect(service.direction()).toBe("ltr");
    expect(service.copy().brand).toBe("Kalm Cafe");
  });

  it("switches Arabic copy to right-to-left direction", () => {
    const service = new LanguageService();

    service.setLanguage("ar");

    expect(service.language()).toBe("ar");
    expect(service.direction()).toBe("rtl");
    expect(service.copy().brand).toBe("كالم كافيه");
  });
});
