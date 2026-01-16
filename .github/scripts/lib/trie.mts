export interface TrieBuilder<T> {
  set(path: string, value: T): void;
  build(): Trie<T>;
}

export interface TrieNode<T> extends Iterable<TrieNode<T>> {
  get prefix(): string;
  get value(): T | undefined;
  get size(): number;
}

class TrieNodeImpl<T> implements TrieNode<T> {
  readonly #prefix: string;
  readonly #children: readonly TrieNode<T>[];
  readonly #value: T | undefined;

  constructor({
    prefix,
    children,
    value,
  }: {
    readonly prefix: string;
    readonly children: readonly TrieNode<T>[];
    readonly value: T | undefined;
  }) {
    this.#prefix = prefix;
    this.#children = children;
    this.#value = value;
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
