def format_brazilian_currency(val: float) -> str:
    """Formats a float as standard Brazilian currency, e.g. 1000.0 -> 'R$ 1.000,00'."""
    formatted = f"{val:,.2f}"
    return "R$ " + formatted.replace(",", "X").replace(".", ",").replace("X", ".")
