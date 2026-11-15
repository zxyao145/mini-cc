import { expect, it } from "vitest";
import type { Tag } from "@/types";
import { tagSuggestions } from "./tag-suggestions";

const [go, gopher, dotnet] = ["Go", "Gopher", ".NET"].map((name): Tag => ({
  id: name, name, color: "#3b82f6", createdAt: "",
}));
const all = [go, gopher, dotnet];
const attached = [dotnet];

it("returns every tag not on the article when the input is blank", () => {
  expect(tagSuggestions(all, attached, "  ")).toEqual({
    options: [go, gopher], exact: null, attachedMatch: false, canCreate: false,
  });
});

it("filters by case-insensitive substring after trimming", () => {
  expect(tagSuggestions(all, attached, " GO ").options).toEqual([go, gopher]);
});

it("finds a case-insensitive exact match and suppresses create", () => {
  expect(tagSuggestions(all, attached, "go")).toMatchObject({ exact: go, canCreate: false });
});

it("flags names already on the article", () => {
  expect(tagSuggestions(all, attached, ".net")).toEqual({
    options: [], exact: null, attachedMatch: true, canCreate: false,
  });
});

it("allows creating a new name when there are only partial matches", () => {
  expect(tagSuggestions(all, attached, "Gop")).toEqual({
    options: [gopher], exact: null, attachedMatch: false, canCreate: true,
  });
  expect(tagSuggestions(all, attached, "Rust")).toEqual({
    options: [], exact: null, attachedMatch: false, canCreate: true,
  });
});

it("allows creating when tags could not load unless the name is already attached", () => {
  expect(tagSuggestions([], attached, "Rust").canCreate).toBe(true);
  expect(tagSuggestions([], attached, ".NET")).toMatchObject({ attachedMatch: true, canCreate: false });
});
