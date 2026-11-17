"use strict";
(function () {
  function initializePaymentChoices() {
    var forms = document.querySelectorAll("[data-payment-checkout]");
    Array.prototype.forEach.call(forms, function (form) {
      var panel = form.querySelector("[data-bank-qr-panel]");
      if (!panel) return;
      var choices = form.querySelectorAll('input[name="PaymentMethod"]');
      function updatePanel() {
        var selected = form.querySelector(
          'input[name="PaymentMethod"]:checked',
        );
        panel.hidden = !selected || selected.value !== "BankTransfer";
      }
      Array.prototype.forEach.call(choices, function (choice) {
        choice.addEventListener("change", updatePanel);
      });
      updatePanel();
    });
  }
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initializePaymentChoices);
  } else {
    initializePaymentChoices();
  }
})();
