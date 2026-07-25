(function () {
    const depositButton = document.getElementById("btnDepositOpen");
    const buyoutButton = document.getElementById("btnBuyoutOpen");
    const overlay = document.getElementById("depositModalOverlay");
    const captchaInput = document.getElementById("captchaInput");

    if (!overlay || !captchaInput || (!depositButton && !buyoutButton)) {
        return;
    }

    injectPhoneField();

    const btnOpen = replaceWithClone("btnDepositOpen");
    const btnBuyoutOpen = replaceWithClone("btnBuyoutOpen");
    const btnClose = replaceWithClone("btnDepositClose");
    const btnGoStep2 = replaceWithClone("btnGoStep2");
    const btnBackStep1 = replaceWithClone("btnBackStep1");
    const btnSubmit = replaceWithClone("btnSubmitDeposit");
    const btnDone = replaceWithClone("btnDepositDone");

    const phoneInput = document.getElementById("customerPhoneInput");
    const phoneHint = document.getElementById("customerPhoneHint");
    const depositError = document.getElementById("depositError");
    const depositErrorMsg = document.getElementById("depositErrorMsg");

    let currentUserProfile = null;
    let currentCarId = null;
    let currentCarPrice = null;
    let modalMode = "deposit";

    btnOpen?.addEventListener("click", () => handlePurchaseIntent("deposit"));
    btnBuyoutOpen?.addEventListener("click", () => handlePurchaseIntent("buyout"));
    btnClose?.addEventListener("click", closeModal);
    btnDone?.addEventListener("click", () => {
        closeModal();
        window.location.reload();
    });
    btnGoStep2?.addEventListener("click", () => showStep(2));
    btnBackStep1?.addEventListener("click", () => showStep(1));
    btnSubmit?.addEventListener("click", submitPurchaseRequest);
    overlay.addEventListener("click", (event) => {
        if (event.target === overlay) {
            closeModal();
        }
    });

    captchaInput.addEventListener("input", () => {
        captchaInput.value = captchaInput.value.toUpperCase();
    });

    const requestedAction = new URLSearchParams(window.location.search).get("chatAction");
    if (requestedAction === "deposit" || requestedAction === "buyout") {
        window.setTimeout(() => handlePurchaseIntent(requestedAction), 150);
    }

    function replaceWithClone(id) {
        const element = document.getElementById(id);
        if (!element) {
            return null;
        }

        const clone = element.cloneNode(true);
        element.parentNode.replaceChild(clone, element);
        return clone;
    }

    function injectPhoneField() {
        if (document.getElementById("customerPhoneInput")) {
            return;
        }

        const loginWarning = document.getElementById("depositLoginWarning");
        if (!loginWarning) {
            return;
        }

        loginWarning.insertAdjacentHTML(
            "afterend",
            `
            <div class="mb-3">
                <label class="fw-bold text-dark mb-2 d-block" style="font-size:14px;">
                    <i class="bi bi-telephone-fill me-1 text-warning"></i> So dien thoai lien he
                </label>
                <input id="customerPhoneInput" type="tel" class="form-control"
                       placeholder="Nhap so dien thoai cua ban" maxlength="20" autocomplete="tel">
                <div id="customerPhoneHint" class="small text-muted mt-2">
                    Showroom se dung so nay de xac nhan giao dich va lien he ban giao xe.
                </div>
            </div>
            `
        );
    }

    async function handlePurchaseIntent(mode) {
        const profile = await getCurrentProfile(true);
        if (!profile) {
            requestLogin(mode);
            return;
        }

        openModal(mode, profile);
    }

    function requestLogin(mode) {
        const url = new URL(window.location.href);
        url.searchParams.set("chatAction", mode);
        window.history.replaceState({}, "", url.pathname + url.search + url.hash);

        const loginModal = document.getElementById("loginModal");
        if (loginModal && window.bootstrap?.Modal) {
            window.bootstrap.Modal.getOrCreateInstance(loginModal).show();
            return;
        }

        window.location.href = `/Auth/Login?ReturnUrl=${encodeURIComponent(url.pathname + url.search + url.hash)}`;
    }

    async function getCurrentProfile(forceRefresh) {
        if (!forceRefresh && currentUserProfile) {
            return currentUserProfile;
        }

        const response = await fetch("/Auth/Profile", {
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });

        const contentType = response.headers.get("content-type") || "";
        if (response.redirected || !contentType.includes("application/json")) {
            currentUserProfile = null;
            return null;
        }

        const data = await response.json();
        if (!response.ok) {
            currentUserProfile = null;
            return null;
        }

        currentUserProfile = {
            userId: readValue(data, "userId", "UserId"),
            fullName: readValue(data, "fullName", "FullName") || "Khach hang",
            email: readValue(data, "email", "Email") || "",
            phoneNumber: readValue(data, "phoneNumber", "PhoneNumber") || ""
        };
        return currentUserProfile;
    }

    function readValue(source, camelKey, pascalKey) {
        return source?.[camelKey] ?? source?.[pascalKey] ?? null;
    }

    function getCurrentCarStatus() {
        const badgeText = (document.querySelector(".badge-status")?.textContent || "").toLowerCase();
        if (badgeText.includes("dat coc") || badgeText.includes("đặt cọc")) {
            return "Reserved";
        }
        if (badgeText.includes("da ban") || badgeText.includes("đã bán")) {
            return "Sold";
        }
        return "Available";
    }

    function openModal(mode, profile) {
        modalMode = mode;
        const sourceButton = mode === "deposit" ? btnOpen : btnBuyoutOpen;
        if (!sourceButton) {
            return;
        }

        const currentCarStatus = getCurrentCarStatus();
        currentCarId = Number(sourceButton.dataset.carid);
        currentCarPrice = Number(sourceButton.dataset.price);
        const carName = sourceButton.dataset.carname || "";
        const buyoutAmount = currentCarStatus === "Reserved"
            ? Math.round(currentCarPrice * 0.95)
            : currentCarPrice;

        document.getElementById("step1CarName").textContent = carName;

        if (mode === "deposit") {
            document.getElementById("step1Title").textContent = "Dat coc giu xe";
            document.getElementById("step1Subtitle").textContent = "Lien he showroom de xac nhan va hoan tat dat coc";
            document.getElementById("step1HotlineDesc").textContent = "Lien he truc tiep de trao doi va dat coc xe";
            document.getElementById("step1AmountLabel").innerHTML = '<i class="bi bi-cash me-1"></i> Tien coc (5%)';
            document.getElementById("step1DepositAmt").textContent = formatVnd(Math.round(currentCarPrice * 0.05));
            document.getElementById("step1ExpiryLabel").innerHTML = '<i class="bi bi-calendar-check me-1"></i> Thoi han giu coc';
            document.getElementById("step1ExpiryVal").textContent = "14 ngay";
            document.getElementById("step1Alert").style.display = "flex";
            document.getElementById("btnGoStep2").innerHTML = '<i class="bi bi-arrow-right-circle me-2"></i> Toi da lien he - Nhap ma xac nhan';
            document.getElementById("step2Title").textContent = "Nhap ma xac nhan";
            document.getElementById("step2Subtitle").textContent = "Nhap ma do nhan vien showroom cung cap qua dien thoai";
            document.getElementById("loginWarningText").textContent = "Ban can dang nhap de dat coc.";
            document.getElementById("btnSubmitText").textContent = "Xac nhan dat coc";
        } else {
            document.getElementById("step1Title").textContent = "Mua dut xe";
            document.getElementById("step1Subtitle").textContent = "Lien he showroom de thanh toan va hoan tat mua dut";
            document.getElementById("step1HotlineDesc").textContent = "Lien he truc tiep de chuyen khoan thanh toan mua dut xe";
            document.getElementById("step1AmountLabel").innerHTML = currentCarStatus === "Reserved"
                ? '<i class="bi bi-cash me-1"></i> So tien con lai phai tra'
                : '<i class="bi bi-cash me-1"></i> Gia ban xe (100%)';
            document.getElementById("step1DepositAmt").textContent = formatVnd(buyoutAmount);
            document.getElementById("step1ExpiryLabel").innerHTML = '<i class="bi bi-truck me-1"></i> Ban giao du kien';
            document.getElementById("step1ExpiryVal").textContent = "Som nhat";
            document.getElementById("step1Alert").style.display = "none";
            document.getElementById("btnGoStep2").innerHTML = '<i class="bi bi-arrow-right-circle me-2"></i> Toi da thanh toan - Nhap ma giao dich';
            document.getElementById("step2Title").textContent = "Nhap ma giao dich";
            document.getElementById("step2Subtitle").textContent = "Nhap ma giao dich do nhan vien cung cap sau khi thanh toan";
            document.getElementById("loginWarningText").textContent = "Ban can dang nhap de mua dut.";
            document.getElementById("btnSubmitText").textContent = "Xac nhan mua dut";
        }

        phoneInput.value = profile?.phoneNumber || "";
        phoneHint.textContent = phoneInput.value
            ? "Ban co the chinh sua so dien thoai neu can."
            : "Vui long nhap so dien thoai truoc khi xac nhan giao dich.";
        captchaInput.value = "";
        hideError();
        showStep(1);
        overlay.classList.add("active");
        document.body.style.overflow = "hidden";
        clearChatActionQuery();
    }

    function closeModal() {
        overlay.classList.remove("active");
        document.body.style.overflow = "";
    }

    function showStep(stepNumber) {
        ["depositStep1", "depositStep2", "depositStep3"].forEach((id, index) => {
            document.getElementById(id)?.classList.toggle("active", index === stepNumber - 1);
        });
    }

    function clearChatActionQuery() {
        const url = new URL(window.location.href);
        if (!url.searchParams.has("chatAction")) {
            return;
        }

        url.searchParams.delete("chatAction");
        window.history.replaceState({}, "", url.pathname + url.search + url.hash);
    }

    function formatVnd(amount) {
        return Number(amount || 0).toLocaleString("vi-VN") + " VND";
    }

    function showError(message) {
        depositErrorMsg.textContent = message;
        depositError.style.display = "block";
    }

    function hideError() {
        depositError.style.display = "none";
        depositErrorMsg.textContent = "";
    }

    async function submitPurchaseRequest() {
        const profile = await getCurrentProfile(true);
        if (!profile?.userId) {
            requestLogin(modalMode);
            return;
        }

        const phoneNumber = phoneInput.value.trim();
        if (!phoneNumber) {
            showError("Vui long nhap so dien thoai lien he.");
            phoneInput.focus();
            return;
        }

        if (!/^[0-9+\-\s]{8,20}$/.test(phoneNumber)) {
            showError("So dien thoai khong hop le.");
            phoneInput.focus();
            return;
        }

        const code = captchaInput.value.trim().toUpperCase();
        if (!code) {
            showError("Vui long nhap ma xac nhan.");
            captchaInput.focus();
            return;
        }

        hideError();
        btnSubmit.disabled = true;
        btnSubmit.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> Dang xu ly...';

        try {
            if (phoneNumber !== (profile.phoneNumber || "")) {
                const updatePhoneResponse = await fetch("/Auth/UpdatePhone", {
                    method: "PUT",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ phoneNumber })
                });
                const updatePhoneData = await updatePhoneResponse.json();
                if (!updatePhoneResponse.ok) {
                    throw new Error(readValue(updatePhoneData, "message", "Message") || "Khong the cap nhat so dien thoai.");
                }
                profile.phoneNumber = readValue(updatePhoneData, "phoneNumber", "PhoneNumber") || phoneNumber;
                currentUserProfile = profile;
            }

            const endpoint = modalMode === "deposit" ? "deposit" : "buyout";
            const payload = {
                carId: currentCarId,
                customerId: profile.userId,
                customerName: profile.fullName || "Khach hang",
                customerPhone: profile.phoneNumber || phoneNumber,
                customerEmail: profile.email || "",
                captchaCode: code
            };

            const response = await fetch(`/Cars/SubmitPurchaseRequest?endpoint=${endpoint}`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            const data = await response.json();
            const success = readValue(data, "success", "Success");
            const message = readValue(data, "message", "Message");
            const depositAmount = readValue(data, "depositAmount", "DepositAmount");
            const depositExpiry = readValue(data, "depositExpiry", "DepositExpiry");
            const requestId = readValue(data, "requestId", "RequestId");

            if (!response.ok || !success) {
                throw new Error(message || "Giao dich khong thanh cong.");
            }

            if (modalMode === "deposit") {
                document.getElementById("step3Title").textContent = "Dat coc thanh cong!";
                document.getElementById("step3Subtitle").textContent = "Xe da duoc giu rieng cho ban.";
                document.getElementById("step3AmtLabel").textContent = "So tien dat coc";
                document.getElementById("step3DepositAmt").textContent = formatVnd(depositAmount);
                document.getElementById("step3ExpiryRow").style.display = "flex";
                if (depositExpiry) {
                    document.getElementById("step3Expiry").textContent = new Date(depositExpiry).toLocaleDateString("vi-VN");
                }
            } else {
                document.getElementById("step3Title").textContent = "Mua dut thanh cong!";
                document.getElementById("step3Subtitle").textContent = "Cam on ban da mua xe tai showroom chung toi.";
                document.getElementById("step3AmtLabel").textContent = "Tong so tien";
                document.getElementById("step3DepositAmt").textContent = formatVnd(depositAmount);
                document.getElementById("step3ExpiryRow").style.display = "none";
            }

            document.getElementById("step3RequestId").textContent = "#" + requestId;
            showStep(3);
        } catch (error) {
            showError(error.message || "Khong the ket noi den may chu. Vui long thu lai.");
        } finally {
            btnSubmit.disabled = false;
            btnSubmit.innerHTML = `<i class="bi bi-lock-fill me-2"></i> <span id="btnSubmitText">${modalMode === "deposit" ? "Xac nhan dat coc" : "Xac nhan mua dut"}</span>`;
        }
    }
})();
