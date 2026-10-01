document.addEventListener("DOMContentLoaded", function () {
    $qar.bindTinymceEditor();
    if (document.querySelectorAll('a[rel="custom-modal"]')) {
        document.querySelectorAll('a[rel="custom-modal"]').forEach((a) => {
            a.addEventListener("click", function (event) {
                event.preventDefault();
                const url = this.getAttribute("href") ? this.getAttribute("href") : "",
                    width = this.getAttribute("data-width")
                        ? this.getAttribute("data-width")
                        : "";
                $qar.showCustomModal(url, width);
            });
        });
    }

    if (document.querySelectorAll("form.qar-form")) {
        document.querySelectorAll("form.qar-form").forEach((form) => {
            form.addEventListener("submit", async function (event) {
                event.preventDefault();
                const form = this,
                    button = form.querySelector('button[type="submit"]'),
                    publishButton = form.querySelector(
                        'button[type="button"].btn-save-publish'
                    ),
                    data = new FormData(form),
                    url = form.getAttribute("action"),
                    method = form.getAttribute("method"),
                    multiLanguageJson = $qar.getMultiLanguageJson(form);

                if (multiLanguageJson) {
                    data.append("multiLanguageJson", multiLanguageJson);
                }
                $qar.setBtnStatus(button, "loading");
                if (publishButton) {
                    $qar.setBtnStatus(publishButton, "loading");
                }
                try {
                    const res = await fetch(url, { method: method, body: data });
                    const resData = await res.json();
                    if (resData["status"] === "success") {
                        $qar.showMessage("success", resData["message"], 1500);
                        if (resData["backUrl"]) {
                            setTimeout(() => {
                                window.location.href = resData["backUrl"];
                            }, 1000);
                        } else {
                            $qar.showMessage("success", resData["message"]);
                        }
                    } else {
                        const input = form.querySelector(
                            '[name="' + resData["data"] + '"]'
                        );
                        if (input) {
                            $qar.showFormInputError(input, resData["message"]);
                        } else {
                            $qar.showMessage(resData["status"], resData["message"]);
                        }
                    }
                } catch (err) {
                    $qar.showMessage("error", err.message);
                } finally {
                    $qar.setBtnStatus(button, "reset");
                    if (publishButton) {
                        $qar.setBtnStatus(publishButton, "reset");
                    }
                }
            });
        });
    }

    if (document.querySelectorAll("a[data-language]")) {
        document.querySelectorAll("a[data-language]").forEach((a) => {
            a.addEventListener("click", function (event) {
                event.preventDefault();
                let aNode = this,
                    pathName = window.location.pathname,
                    currentLanguge = $qar.getCurrentLanguage(),
                    dataLanguage = aNode.getAttribute("data-language")
                        ? aNode.getAttribute("data-language").toLowerCase()
                        : "";
                if (currentLanguge === dataLanguage) return;
                if (!pathName) {
                    window.location.href = "/" + dataLanguage + window.location.search;
                }
                if (pathName && pathName.length < currentLanguge.length) return;
                window.location.href =
                    "/" +
                    dataLanguage +
                    pathName.substring(currentLanguge.length + 1) +
                    window.location.search;
            });
        });
    }

    if (document.querySelectorAll("a[data-skin]")) {
        document.querySelectorAll("a[data-skin]").forEach((a) => {
            a.addEventListener("click", async function (event) {
                event.preventDefault();
                const dataSkin = this.getAttribute("data-skin")
                    ? this.getAttribute("data-skin").toLowerCase()
                    : "",
                    data = new FormData();
                data.append("skinName", dataSkin);
                const inputs = document.querySelectorAll("input[type='search']")
                if (inputs.length > 0) {
                    // $qar.ChangeInputMode(inputs);
                }
                try {
                    const res = await fetch(
                        "/" + $qar.getCurrentLanguage() + "/Admin/SaveSkin",
                        { method: "POST", body: data }
                    );
                    const resData = await res.json();
                    if (resData["status"] === "success") {
                        if (dataSkin === "light" || dataSkin === "dark") {
                            layout_change(dataSkin);
                        }
                    } else {
                        $qar.showMessage(resData["status"], resData["message"]);
                    }
                } catch (err) {
                    $qar.showMessage("error", err.message);
                }
            });
        });
    }

    if (document.querySelector(".datatable-checkbox-all")) {
        document
            .querySelector(".datatable-checkbox-all")
            .addEventListener("change", function (e) {
                const table = this.closest("table"),
                    checkboxs = table.querySelectorAll("tbody input.datatable-checkbox");
                if (!checkboxs) return;
                checkboxs.forEach((elem) => {
                    elem.checked = e.target.checked;
                });
            });
    }

    if (document.querySelectorAll("button[data-qar-confirm]")) {
        document.querySelectorAll("button[data-qar-confirm]").forEach((elem) => {
            elem.addEventListener("click", function () {
                const okText = this.getAttribute("data-qar-ok"),
                    canceText = this.getAttribute("data-qar-cancel"),
                    title = this.getAttribute("data-qar-title"),
                    url = this.getAttribute("data-qar-confirm"),
                    tableId = this.getAttribute("data-qar-tableid"),
                    checkboxs = document.querySelectorAll(
                        "#" + tableId + " input.datatable-checkbox"
                    );

                const swalWithBootstrapButtons = Swal.mixin({
                    customClass: {
                        confirmButton: "btn btn-success",
                        cancelButton: "btn btn-danger",
                    },
                    buttonsStyling: false,
                });
                swalWithBootstrapButtons
                    .fire({
                        title: title,
                        icon: "warning",
                        showCancelButton: true,
                        confirmButtonText: okText,
                        cancelButtonText: canceText,
                        reverseButtons: true,
                    })
                    .then(async (result) => {
                        if (result.isConfirmed) {
                            try {
                                const formData = new FormData();
                                formData.append("manageType", "delete");
                                if (checkboxs) {
                                    checkboxs.forEach((elem) => {
                                        if (elem.checked) {
                                            formData.append(
                                                "idList[]",
                                                parseInt(elem.getAttribute("data-id"))
                                            );
                                        }
                                    });
                                }
                                const res = await fetch(url, {
                                    method: "POST",
                                    body: formData,
                                });
                                const resData = await res.json();
                                $qar.showMessage(resData["status"], resData["message"]);
                                if (resData["status"] === "success") {
                                    setTimeout(() => {
                                        window.location.reload();
                                    }, 1000);
                                }
                            } catch (err) {
                                $qar.showMessage("error", err.message);
                            }
                        }
                    });
            });
        });
    }




});