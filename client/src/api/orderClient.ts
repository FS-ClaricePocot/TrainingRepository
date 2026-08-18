// Typed client for the Orders endpoints, built against orders-api.yml


import type { Order, OrderStatus } from "../types/order";

const BASE_URL = "https://localhost:5001";


export type ApiError =
  | { type: "network"; message: string }
  | { type: "badRequest"; message: string }
  | { type: "notFound"; message: string }
  | { type: "server"; httpStatus: number; message: string }
  | { type: "parse"; message: string };


export type RequestState<T> =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "success"; data: T }
  | { status: "error"; error: ApiError };

async function toApiError(response: Response): Promise<ApiError> {
  let message = response.statusText;
  try {
    const body: unknown = await response.json();
    if (
      typeof body === "object" &&
      body !== null &&
      "message" in body &&
      typeof (body as { message: unknown }).message === "string"
    ) {
      message = (body as { message: string }).message;
    }
  } catch {
    // Error response body wasn't valid JSON — fall back to statusText set above.
  }

  if (response.status === 400) return { type: "badRequest", message };
  if (response.status === 404) return { type: "notFound", message };
  return { type: "server", httpStatus: response.status, message };
}

function toParseError(err: unknown): { status: "error"; error: ApiError } {
  return {
    status: "error",
    error: {
      type: "parse",
      message: err instanceof Error ? err.message : "Failed to parse response body",
    },
  };
}

function toNetworkError(err: unknown): { status: "error"; error: ApiError } {
  return {
    status: "error",
    error: {
      type: "network",
      message: err instanceof Error ? err.message : "Unknown network error",
    },
  };
}

export async function getOrdersForCustomer(
  customerId: number,
  status: OrderStatus
): Promise<RequestState<Order[]>> {
  let response: Response;
  try {
    const url =
      `${BASE_URL}/api/orders` +
      `?customerId=${encodeURIComponent(customerId)}` +
      `&status=${encodeURIComponent(status)}`;
    response = await fetch(url);
  } catch (err) {
    return toNetworkError(err);
  }

  if (!response.ok) {
    return { status: "error", error: await toApiError(response) };
  }

  try {
    const data = (await response.json()) as Order[];
    return { status: "success", data };
  } catch (err) {
    return toParseError(err);
  }
}

export interface TotalSpendResponse {
  customerId: number;
  totalSpend: number;
}


export async function getTotalSpend(
  customerId: number
): Promise<RequestState<TotalSpendResponse>> {
  let response: Response;
  try {
    const url = `${BASE_URL}/api/customers/${encodeURIComponent(customerId)}/total-spend`;
    response = await fetch(url);
  } catch (err) {
    return toNetworkError(err);
  }

  if (!response.ok) {
    return { status: "error", error: await toApiError(response) };
  }

  try {
    const data = (await response.json()) as TotalSpendResponse;
    return { status: "success", data };
  } catch (err) {
    return toParseError(err);
  }
}


export async function updateOrderStatus(
  orderId: number,
  status: OrderStatus
): Promise<RequestState<void>> {
  try {
    const url = `${BASE_URL}/api/orders/${encodeURIComponent(orderId)}/status`;
    const response = await fetch(url, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ status }),
    });

    if (!response.ok) {
      return { status: "error", error: await toApiError(response) };
    }

    return { status: "success", data: undefined };
  } catch (err) {
    return toNetworkError(err);
  }
}