namespace Backend;

public static class ExpressionEvaluator
{
    public static double Evaluate(string infix) => EvaluatePostfix(ToPostfix(Tokenize(infix)));

    /** 
      * Single place that builds the error used everywhere in this class.
      * Called as 'throw InvalidExpression();' both in normal statements and
      * inside switch expressions (a throw expression just needs something
      * that evaluates to an Exception).
     */
    private static Exception InvalidExpression() => new Exception("Invalid expression");

    /**
     * Tokenizer step. The original code read the expression character by 
     * characterm so multi-digit numbers (e.g. "12") and decimals
     * (e.g. "3.5") were split into separate single-character tokens and broke
     * the evaluation. This groups consecutive digits/dots into one full
     * number token before any stack logic runs.
     */
    private static List<string> Tokenize(string infix)
    {
        var tokens = new List<string>();
        var i = 0;

        while (i < infix.Length)
        {
            var c = infix[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (IsOperator(c))
            {
                tokens.Add(c.ToString());
                i++;
            }
            else if (char.IsDigit(c) || c == '.')
            {
                var start = i;
                var dotCount = 0;
                while (i < infix.Length && (char.IsDigit(infix[i]) || infix[i] == '.'))
                {
                    if (infix[i] == '.') dotCount++;
                    i++;
                }

                /**
                 * A number token must have at most one decimal point and at
                 * least one digit (e.g. "3.5" is valid, "3..5" and "." are
                 * not). Catching this hare gives a clear "Invalid
                 * expression" instead of double. Parse throwing its own
                 * FormatException later
                 */
                var numberToken = infix.Substring(start, i - start);
                if (dotCount > 1 || numberToken == ".")
                    throw InvalidExpression();
                tokens.Add(numberToken);
            }
            else
                throw InvalidExpression();
        }
        return tokens;
    }

    /** 
      * Shunting-Yard: builds the postfix (RPN) expression using an operator
      * stack (Stack<string>). Consumes complete tokens (not single chars) so
      * multi-digit and decimal numbers stay intact. The output is a plain
      * List<string> - only the operator stack needs to be a stack.
     */
    private static List<string> ToPostfix(List<string> tokens)
    {
        var postfix = new List<string>();
        var stack = new Stack<string>();

        foreach (var token in tokens)
        {
            if (!IsOperatorToken(token, out var op))
            {
                postfix.Add(token);
                continue;
            }

            if (op == "(") stack.Push(op);
            else if (op == ")")
            {
                // Pop everything until the matching '('
                PopWhile(stack, postfix, top => true);

                if (stack.Count == 0) throw InvalidExpression();
                stack.Pop(); // discard the '('
            }
            else
            {
                // '^' is right-associative, so on a priority tie we do NOT
                // pop (unlike + - * /).
                PopWhile(stack, postfix, top => Priority(top) > Priority(op) || (Priority(top) == Priority(op) && op != "^"));
                stack.Push(op);
            }
        }

        // NOTE: this must run once, after the foreach above finishes - not
        // once per token. keep it outside the foreach's braces.
        while (stack.Count != 0)
        {
            if (stack.Peek() == "(") throw InvalidExpression();
            postfix.Add(stack.Pop());
        }

        return postfix;
    }

    /** Shared by the ')' case and the operator case in ToPostfix: pops from 
      * the operator stack into postfix, stopping at '(' or when the stack 
      * empties, or as soon as shouldPop says to stop.
    */
    private static void PopWhile(Stack<string> stack, List<string> postfix, Func<string, bool> shouldPop)
    {
        while (stack.Count > 0 && stack.Peek() != "(" && shouldPop(stack.Peek())) postfix.Add(stack.Pop());
    }

    // Operator precedence. Never called with '(' or ')' - those are
    // handle separately in ToPostfix, not through priority comparison.
    private static int Priority(string op) => op switch
    {
        "^" => 3,
        "*" => 2,
        "/" => 2,
        "+" => 1,
        "-" => 1,
        _ => throw InvalidExpression(),
    };

    private static bool IsOperator(char item) => item == '^' || item == '*' || item == '/' || item == '+' || item == '-' || item == '(' || item == ')';

    // Helper so the "is this token a single-char operator?" check
    // (needed in both ToPostfix and EvaluatePostfix) lives in one place.
    private static bool IsOperatorToken(string token, out string op)
    {
        if (token.Length == 1 && IsOperator(token[0]))
        {
            op = token;
            return true;
        }
        op = string.Empty;
        return false;
    }

    /** Evaluates the postfix expression using the value stack (Stack<double>).
      * Uses double.Parse on the full token instead of car.GetNumericValue
      * on a single digit, so multi-digit and decimal numbers are parsed
      * correctly.
     */
    private static double EvaluatePostfix(List<string> postfix)
    {
        var stack = new Stack<double>();
        foreach (var token in postfix)
        {
            if (IsOperatorToken(token, out var op))
            {
                /*
                 * An operator needs two operands already on the stack. If
                 * there aren't two, the expression was malformed upstream
                 * (e.g. "()" produces an empty postfix list, or something
                 * like "5+" is missing an operand) - report it the some
                 * way as any other invalid expression instead of letting
                 * Stack<double>.Pop() throw its own exception.
                */
                if (stack.Count < 2) throw InvalidExpression();

                var ope2 = stack.Pop();
                var ope1 = stack.Pop();
                stack.Push(Calculator(ope1, ope2, op));
            }
            else stack.Push(double.Parse(token, System.Globalization.CultureInfo.InvariantCulture));
        }

        /*
         * A valid expression always reduces to exactly one value. Zero
         * (e.g. "()") or more than one (e.g. "3 4" with no operator between
         * them) means the expression was invalid.
        */
        if (stack.Count != 1) throw InvalidExpression();
        return stack.Pop();
    }

    private static double Calculator(double ope1, double ope2, string item) => item switch
    {
        "*" => ope1 * ope2,
        "/" => ope1 / ope2,
        "+" => ope1 + ope2,
        "-" => ope1 - ope2,
        "^" => Math.Pow(ope1, ope2),
        _ => throw InvalidExpression(),
    };
}
