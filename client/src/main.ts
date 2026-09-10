import { getOrdersForCustomer } from "./api/orderClient";

const app = document.querySelector<HTMLDivElement>("#app")!;

function renderLine(text: string): void {
  const p = document.createElement("p");
  p.textContent = text;
  app.appendChild(p);
  console.log(text);
}

async function fetchAndRender(label: string, status: "Completed" | "Pending"): Promise<void> {
  renderLine(`Fetching ${status} orders for customer 1...`);

  // test customerId 1 from local db
  const result = await getOrdersForCustomer(1, status);

  if (result.status === "success") {
    renderLine(`${label}: ${result.data.length} order(s)`);
    for (const order of result.data) {
      renderLine(`  OrderId=${order.orderId}, Total=${order.total}, Status=${order.status}`);
    }
  } else if (result.status === "error") {
    renderLine(`${label}: request failed — ${result.error.type}: ${result.error.message}`);
  } else {
    renderLine(`${label}: unexpected state "${result.status}"`);
  }
}

async function verifyCacheFix(): Promise<void> {
  // Same sequence as in tools/CacheRepro, this time through the
  // real endpoint: fetch Completed, then Pending, for the same customer.
  await fetchAndRender("COMPLETED", "Completed");
  await fetchAndRender("PENDING", "Pending");
}

verifyCacheFix();
