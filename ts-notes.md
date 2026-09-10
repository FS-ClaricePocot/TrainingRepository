# ts-notes.md — What strict mode catches that Razor/C# never surfaced


## It has the advantage of compile-time typo/incorrect string checking

`Order.Status` in C# is a plain `string`, same as
`UpdateOrderStatus(int, string newStatus)`'s parameter. Nothing stops
`UpdateOrderStatus(42, "Compelted")` from compiling and silently writing
bad data — C#'s type system doesn't check string *contents*, and Razor
model binding never surfaced this either.

## It can be leverage for exhaustiveness

`OrderStatus` only allows six specific values, not any random string. That
means if I write a `switch` that handles all six, and someone later adds a
seventh status, TypeScript can warn me that my `switch` is now missing a
case for it — I don't have to notice the gap myself.

C#'s `Status` is just a `string`, so there's no fixed list for the
compiler to check against. It can never tell me "you forgot to handle a
status" because as far as C# is concerned, a string is a string. Any
code that branches on `Status` today only stays complete because a
developer remembers to update every branch by hand — not because the
compiler enforces it.