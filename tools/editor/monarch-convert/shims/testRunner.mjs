// Records testTokenization calls instead of running them.
export const recorded = [];
export function testTokenization(language, tests) {
	recorded.push({ languages: typeof language === 'string' ? [language] : language, tests });
}
// types some test files import as values (TypeScript erases them; the shim has to provide the names)
export const ITestItem = undefined;
export const IRelaxedToken = undefined;
