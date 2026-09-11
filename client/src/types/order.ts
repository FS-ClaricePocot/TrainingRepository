export type OrderStatus = 
    | "Pending"
    | "Processing"
    | "Shipped"
    | "Completed"
    | "Cancelled"
    | "Refunded";

export interface Order {
    orderId: number;
    customerId: number;
    total: number;
    status: OrderStatus;
}

