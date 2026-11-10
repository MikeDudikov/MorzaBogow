"""Outcomes a stage can raise. The worker turns each into a job state."""


class PipelineError(Exception):
    """A stage cannot continue; the job is marked failed at this stage."""

    state = "failed"

    def __init__(self, message: str):
        super().__init__(message)
        self.message = message


class Fatal(PipelineError):
    """Bad input or a provider rejection that a retry will not fix."""


class NeedsConfiguration(PipelineError):
    """A tool, credential or file is missing. The job waits for `resume` after setup."""

    state = "needs_configuration"

    def __init__(self, message: str, items=None):
        super().__init__(message)
        self.items = list(items or [message])


class NeedsReview(PipelineError):
    """A person has to look: a result, a cost approval, a manual hand-off or an unknown submission."""

    state = "needs_review"


class Retryable(PipelineError):
    """A temporary failure: the same step runs again after a delay."""

    state = "retry"

    def __init__(self, message: str, retry_after=None):
        super().__init__(message)
        self.retry_after = retry_after


class RateLimited(Retryable):
    """HTTP 429 or a full provider queue."""


class Ambiguous(PipelineError):
    """A paid request may or may not have been accepted (timeout after sending, 5xx on create).

    The step never sends it again on its own: it reconciles with the provider's task list first.
    """

    state = "ambiguous"
