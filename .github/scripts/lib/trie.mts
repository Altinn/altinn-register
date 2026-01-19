export interface TrieBuilder<T> {
  set(path: string, value: T): void;
  build(): Trie<T>;
}

export interface TrieNode<T> extends Iterable<TrieNode<T>> {
  get prefix(): string;
  get value(): T | undefined;
  get size(): number;
  get branches(): number;
  get minLength(): number;
  get maxLength(): number;
}

class TrieNodeImpl<T> implements TrieNode<T> {
  readonly #prefix: string;
  readonly #children: readonly TrieNode<T>[];
  readonly #value: T | undefined;
  readonly #minLength: number;
  readonly #maxLength: number;

  static #computeMinLenght<T>(
    prefix: string,
    children: readonly TrieNode<T>[],
    value: T | undefined,
  ): number {
    if (typeof value !== "undefined") {
      return 0;
    }

    let min = Number.POSITIVE_INFINITY;
    for (const child of children) {
      min = Math.min(min, child.prefix.length + child.minLength);
    }

    return min;
  }

  static #computeMaxLenght<T>(
    prefix: string,
    children: readonly TrieNode<T>[],
    value: T | undefined,
  ): number {
    let max = 0;
    for (const child of children) {
      max = Math.max(max, child.prefix.length + child.maxLength);
    }

    return max;
  }

  constructor({
    prefix,
    children,
    value,
  }: {
    readonly prefix: string;
    readonly children: readonly TrieNode<T>[];
    readonly value: T | undefined;
  }) {
    if (typeof value === "undefined" && children.length === 0) {
      throw new Error("A trie node must have either a value or children");
    }

    this.#prefix = prefix;
    this.#children = children;
    this.#value = value;
    this.#minLength = TrieNodeImpl.#computeMinLenght(prefix, children, value);
    this.#maxLength = TrieNodeImpl.#computeMaxLenght(prefix, children, value);
  }

  get value(): T | undefined {
    return this.#value;
  }

  get prefix(): string {
    return this.#prefix;
  }

  get size(): number {
    return this.#children.length;
  }

  get branches(): number {
    const selfBranch = typeof this.#value !== "undefined" ? 1 : 0;
    return selfBranch + this.#children.length;
  }

  get minLength(): number {
    return this.#minLength;
  }

  get maxLength(): number {
    return this.#maxLength;
  }

  [Symbol.iterator](): Iterator<TrieNode<T>> {
    return this.#children[Symbol.iterator]();
  }
}

export class Trie<T> implements TrieNode<T> {
  static builder<T>(): TrieBuilder<T> {
    return new TrieBuilderImpl<T>();
  }

  static from<T>(entries: Iterable<[string, T]>): Trie<T> {
    const builder = Trie.builder<T>();
    for (const [path, value] of entries) {
      builder.set(path, value);
    }

    return builder.build();
  }

  readonly #root: TrieNode<T>;

  constructor(root: TrieNode<T>) {
    this.#root = root;
  }

  get value(): T | undefined {
    return this.#root.value;
  }

  get prefix(): string {
    return this.#root.prefix;
  }

  get size(): number {
    return this.#root.size;
  }

  get branches(): number {
    return this.#root.branches;
  }

  get minLength(): number {
    return this.#root.minLength;
  }

  get maxLength(): number {
    return this.#root.maxLength;
  }

  [Symbol.iterator](): Iterator<TrieNode<T>, any, any> {
    return this.#root[Symbol.iterator]();
  }
}

class TrieBuilderNode<T> implements Iterable<[string, TrieBuilderNode<T>]> {
  readonly #children: Map<string, TrieBuilderNode<T>> = new Map();
  #value: T | undefined = undefined;

  getOrCreateChild(segment: string): TrieBuilderNode<T> {
    let child = this.#children.get(segment);
    if (!child) {
      child = new TrieBuilderNode<T>();
      this.#children.set(segment, child);
    }

    return child;
  }

  get value(): T | undefined {
    return this.#value;
  }

  set value(value: T) {
    if (this.#value !== undefined) {
      throw new Error("Value already set for this node");
    }

    this.#value = value;
  }

  get size(): number {
    return this.#children.size;
  }

  single(): [string, TrieBuilderNode<T>] {
    if (this.#children.size !== 1) {
      throw new Error("Node does not have exactly one child");
    }

    return this.#children.entries().next().value!;
  }

  [Symbol.iterator](): Iterator<[string, TrieBuilderNode<T>]> {
    return this.#children[Symbol.iterator]();
  }
}

const validRegex = /^[a-zA-Z0-9-]+$/;

class TrieBuilderImpl<T> implements TrieBuilder<T> {
  readonly #root: TrieBuilderNode<T> = new TrieBuilderNode<T>();

  set(path: string, value: T): void {
    if (!validRegex.test(path)) {
      throw new Error(`Invalid path: ${path}`);
    }

    const segments = [...path];
    this.#insert(segments, value);
  }

  build(): Trie<T> {
    const root = TrieBuilderImpl.#flatten("", this.#root);
    return new Trie<T>(root);
  }

  #insert(segments: string[], value: T): void {
    let node = this.#root;

    for (const segment of segments) {
      node = node.getOrCreateChild(segment);
    }

    node.value = value;
  }

  static #flatten<T>(prefix: string, node: TrieBuilderNode<T>): TrieNode<T> {
    let sb: string;
    const children: TrieNode<T>[] = [];

    for (let [key, child] of node) {
      sb = key;
      while (child.size === 1 && typeof child.value === "undefined") {
        const [nextKey, nextValue] = child.single();
        sb += nextKey;
        child = nextValue;
      }

      children.push(TrieBuilderImpl.#flatten(sb, child));
    }

    children.sort((a, b) => a.prefix.length - b.prefix.length);

    return new TrieNodeImpl<T>({
      prefix,
      value: node.value,
      children,
    });
  }
}
