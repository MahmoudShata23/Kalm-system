import { HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { CsrfTokenStore } from "./csrf-token.store";

const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);

export const csrfInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(CsrfTokenStore).token();
  const unsafeSameOriginApiRequest = request.url.startsWith("/api/") && !SAFE_METHODS.has(request.method.toUpperCase());
  return next(unsafeSameOriginApiRequest && token
    ? request.clone({ setHeaders: { "X-XSRF-TOKEN": token } })
    : request);
};
