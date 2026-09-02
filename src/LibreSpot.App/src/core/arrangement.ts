export type ArrangementItem = {
  id: string;
  label: string;
};

export type ArrangementResult = {
  items: ArrangementItem[];
  applied: boolean;
};

type IdentifiedElement = ArrangementItem & {
  element: HTMLElement;
  naturalOrder: number;
};

const NATURAL_ORDER_ATTRIBUTE = "data-librespot-natural-order";

function normalized(value: string | null | undefined): string {
  return (value ?? "").replace(/\s+/g, " ").trim();
}

function elementChildren(parent: Element): HTMLElement[] {
  return Array.from(parent.children).filter(
    (element): element is HTMLElement => element.nodeType === 1,
  );
}

function rememberNaturalOrder(
  parent: HTMLElement,
  elements: readonly HTMLElement[],
): Map<HTMLElement, number> {
  let next = Number(parent.dataset.librespotNextNaturalOrder ?? "0");
  if (!Number.isFinite(next) || next < 0) {
    next = 0;
  }
  const orders = new Map<HTMLElement, number>();
  for (const element of elements) {
    const storedValue = element.getAttribute(NATURAL_ORDER_ATTRIBUTE);
    const stored = storedValue === null ? Number.NaN : Number(storedValue);
    const order = Number.isFinite(stored) && stored >= 0 ? stored : next++;
    element.setAttribute(NATURAL_ORDER_ATTRIBUTE, String(order));
    orders.set(element, order);
  }
  parent.dataset.librespotNextNaturalOrder = String(next);
  return orders;
}

function desiredRank(
  item: ArrangementItem,
  desiredOrder: readonly string[],
): number {
  const identities = [item.id, item.label].map((value) =>
    normalized(value).toLocaleLowerCase(),
  );
  const index = desiredOrder.findIndex((value) =>
    identities.includes(normalized(value).toLocaleLowerCase()),
  );
  return index === -1 ? Number.POSITIVE_INFINITY : index;
}

function arrange(
  parent: HTMLElement,
  identified: readonly IdentifiedElement[],
  desiredOrder: readonly string[],
): ArrangementResult {
  const ordered = [...identified].sort((left, right) => {
    const rankDifference =
      desiredRank(left, desiredOrder) - desiredRank(right, desiredOrder);
    return rankDifference || left.naturalOrder - right.naturalOrder;
  });
  const current = identified.map((item) => item.element);
  const applied = ordered.some((item, index) => item.element !== current[index]);
  if (applied) {
    parent.append(...ordered.map((item) => item.element));
  }
  return {
    items: ordered.map(({ id, label }) => ({ id, label })),
    applied,
  };
}

function homeIdentity(section: HTMLElement): ArrangementItem | null {
  const heading = section.querySelector<HTMLElement>(
    "h1, h2, h3, [role=heading]",
  );
  const ariaLabel = normalized(section.getAttribute("aria-label"));
  const label = normalized(heading?.textContent) || ariaLabel;
  const id = ariaLabel || label;
  return id && label ? { id, label } : null;
}

export function applyHomeArrangement(
  document: Document,
  desiredOrder: readonly string[],
): ArrangementResult {
  const parent = document.querySelector<HTMLElement>(".main-home-content");
  if (!parent) {
    return { items: [], applied: false };
  }
  const sections = elementChildren(parent).filter(
    (element) => element.tagName === "SECTION",
  );
  const naturalOrders = rememberNaturalOrder(parent, sections);
  const identified = sections.flatMap((section) => {
    const item = homeIdentity(section);
    return item
      ? [{ ...item, element: section, naturalOrder: naturalOrders.get(section) ?? 0 }]
      : [];
  });
  return arrange(parent, identified, desiredOrder);
}

function sidebarIdentity(
  document: Document,
  row: HTMLElement,
): ArrangementItem | null {
  const group = row.querySelector<HTMLElement>("[aria-labelledby]");
  const labelledBy = normalized(group?.getAttribute("aria-labelledby"));
  const labelledElement = labelledBy ? document.getElementById(labelledBy) : null;
  const label =
    normalized(labelledElement?.textContent) ||
    normalized(row.getAttribute("aria-label"));
  const id =
    labelledBy.replace(/^listrow-title-/, "") ||
    normalized(row.querySelector<HTMLAnchorElement>("a[href]")?.getAttribute("href")) ||
    label;
  return id && label ? { id, label } : null;
}

export function applySidebarArrangement(
  document: Document,
  desiredOrder: readonly string[],
): ArrangementResult {
  const grid = document.querySelector<HTMLElement>(
    '.Root__nav-bar [role="grid"][aria-label="Your Library"]',
  );
  const firstRow = grid?.querySelector<HTMLElement>(
    '[role="row"][aria-selected]',
  );
  const parent = firstRow?.parentElement;
  if (!grid || !firstRow || !parent) {
    return { items: [], applied: false };
  }
  const rows = elementChildren(parent).filter(
    (element) =>
      element.getAttribute("role") === "row" &&
      element.hasAttribute("aria-selected"),
  );
  const naturalOrders = rememberNaturalOrder(parent, rows);
  const identified = rows.flatMap((row) => {
    const item = sidebarIdentity(document, row);
    return item
      ? [{ ...item, element: row, naturalOrder: naturalOrders.get(row) ?? 0 }]
      : [];
  });
  const result = arrange(parent, identified, desiredOrder);
  for (const [index, item] of result.items.entries()) {
    const row = identified.find((candidate) => candidate.id === item.id)?.element;
    row?.setAttribute("aria-rowindex", String(index + 1));
  }
  return result;
}
