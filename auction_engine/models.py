from dataclasses import dataclass, field
from typing import List, Dict, Any


@dataclass
class BidResult:
    """Represents the parsed outcome of an auction voice utterance."""
    value: float
    currency: str
    has_addition: bool
    has_multiplication: bool
    raw_text: str
    cleaned_text: str
    is_valid: bool = True
    action_type: str = "BID"  # "BID", "PASS", "MULTIPLY_ONLY", "NONE"
    numbers_found: List[float] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "raw_transcript": self.raw_text,
            "cleaned_text": self.cleaned_text,
            "value": self.value,
            "currency": self.currency,
            "has_addition": self.has_addition,
            "has_multiplication": self.has_multiplication,
            "is_valid": self.is_valid,
            "action_type": self.action_type,
            "numbers_found": self.numbers_found,
        }
