import type { Tag } from "@/types";

export function tagSuggestions(all: Tag[], attached: Tag[], input: string): {
  options: Tag[];
  exact: Tag | null;
  attachedMatch: boolean;
  canCreate: boolean;
} {
  const query = input.trim().toLocaleLowerCase();
  const attachedIds = new Set(attached.map(tag => tag.id));
  const available = all.filter(tag => !attachedIds.has(tag.id));
  const exact = query ? available.find(tag => tag.name.toLocaleLowerCase() === query) ?? null : null;
  const attachedMatch = !!query && attached.some(tag => tag.name.toLocaleLowerCase() === query);

  return {
    options: query ? available.filter(tag => tag.name.toLocaleLowerCase().includes(query)) : available,
    exact,
    attachedMatch,
    canCreate: !!query && !attachedMatch && !exact,
  };
}
