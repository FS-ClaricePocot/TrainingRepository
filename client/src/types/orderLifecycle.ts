interface BaseOrder {
    orderId: number;
    customerId: number;
    total: number;
}

export interface PendingOrder extends BaseOrder {
    status: "Pending";
}

export interface CompletedOrder extends BaseOrder {
    status: "Completed";
    completedAt: string; //ISO date string
}

export interface CancelledOrder extends BaseOrder {
    status: "Cancelled";
    cancelledAt: string; //ISO date string
    reason?: string;
}

export type OrderLifecycle = PendingOrder | CompletedOrder | CancelledOrder;

type OrderTransitionMap = {
    Pending: "Completed" | "Cancelled";
    // Completed and Cancelled have no keys since they "terminal".
    // There are no valid states they can transition to. 
}

export function transition(order: PendingOrder, to: "Completed") : CompletedOrder;
export function transition(order: PendingOrder, to: "Cancelled", reason?: string) : CancelledOrder;
export function transition(order: PendingOrder, to: OrderTransitionMap["Pending"], reason?: string) : CompletedOrder | CancelledOrder {
    const base: BaseOrder = {
        orderId: order.orderId, 
        customerId: order.customerId, 
        total: order.total
    };

    if (to === "Completed") {
        return {...base, status: "Completed", completedAt: new Date().toISOString()};
    }

    return { ...base, status: "Cancelled", cancelledAt: new Date().toISOString(), reason};
}
