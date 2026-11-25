// Records registerLanguage calls instead of registering with an editor.
export const registered = [];
export function registerLanguage(def) {
	registered.push(def);
}
export async function loadLanguage() {}
